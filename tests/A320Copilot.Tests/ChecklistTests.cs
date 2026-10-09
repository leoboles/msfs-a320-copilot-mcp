using A320Copilot.Bridge;
using A320Copilot.Mcp;
using Xunit;

namespace A320Copilot.Tests;

public sealed class ChecklistTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "a320-checklist-tests-" + Guid.NewGuid().ToString("N"));
    private ChecklistStore Store => new(directory);
    private static ChecklistEvidence User(string note = "Confirmed visually by the user") => new("user", DateTimeOffset.UtcNow, note);

    [Fact]
    public void PersistsAcrossRestartAndRejectsRevisionConflict()
    {
        var initial = Store.Start("pushback", "test flight");
        var updated = Store.Update(initial.Session.Id, 0, "doors_equipment", "confirmed", User());
        Assert.Equal("tug_ready", Store.Get(initial.Session.Id).NextItem!.Id);
        Assert.Equal(1, updated.Session.Revision);
        Assert.Throws<InvalidOperationException>(() => Store.Update(initial.Session.Id, 0, "tug_ready", "confirmed", User()));
        Assert.DoesNotContain(Store.Start("pushback", "new flight").Session.Entries, e => e.Status != "pending");
    }

    [Fact]
    public void CannotJumpPastPrerequisiteAndSkipIsNotConfirmation()
    {
        var initial = Store.Start("pushback", "test");
        Assert.Throws<InvalidOperationException>(() => Store.Update(initial.Session.Id, 0, "brake_released", "confirmed", User()));
        var current = initial;
        foreach (var item in initial.Session.Template.Items)
            current = Store.Update(current.Session.Id, current.Session.Revision, item.Id, "skipped", User("User explicitly skipped in simulator demonstration"));
        Assert.Null(current.NextItem);
        Assert.False(current.AllItemsConfirmed);
        Assert.All(current.Session.Entries, e => Assert.Equal("skipped", e.Status));
    }

    [Fact]
    public void ReopeningPrerequisiteResetsLaterProgress()
    {
        var first = Store.Start("pushback", "test");
        var next = Store.Update(first.Session.Id, 0, "doors_equipment", "confirmed", User());
        Store.Update(first.Session.Id, 1, "tug_ready", "confirmed", User());
        var reset = Store.Update(first.Session.Id, 2, "doors_equipment", "pending", null);
        Assert.All(reset.Session.Entries, e => Assert.Equal("pending", e.Status));
    }

    [Fact]
    public async Task TelemetryCannotConfirmFireTestOrMockSwitch()
    {
        var reader = new TelemetryReader(new TelemetrySettings { Mode = "Mock" }, new DemoAircraftStateSource());
        var tools = new ChecklistTools(Store, reader);
        var session = Store.Start("preparation", "test");
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() => tools.Update(session.Session.Id, 0, "apu_fire_test", "confirmed", "telemetry", "test", default));
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() => tools.Update(session.Session.Id, 0, "parking_brake", "confirmed", "telemetry", "test", default));
        Assert.Equal(0, Store.Get(session.Session.Id).Session.Revision);
    }

    [Fact]
    public async Task RealTelemetryConfirmsMatchingSwitchAndRejectsMismatch()
    {
        foreach (var value in new[] { 0d, 1d })
        {
            var sample = new A320Copilot.Domain.AircraftState(DateTimeOffset.UtcNow, "FlyByWire A320", 0, 0, 0, true)
            {
                Systems = new("test", new Dictionary<string, A320Copilot.Domain.AircraftParameter>(), new Dictionary<string, A320Copilot.Domain.AircraftParameter>())
                {
                    Controls = new Dictionary<string, A320Copilot.Domain.AircraftParameter> { ["ParkingBrakeLever"] = new(value, "bool", "L:A32NX_PARK_BRAKE_LEVER_POS") { Quality = "known", QualityReason = "Validated synthetic test fixture" } }
                }
            };
            using var monitor = new SimConnectAircraftStateSource(new(), () => new Session(sample));
            var tools = new ChecklistTools(Store, new TelemetryReader(new TelemetrySettings { Mode = "Real" }, monitor));
            var started = Store.Start("preparation", "test");
            if (value == 0)
            {
                await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() => tools.Update(started.Session.Id, 0, "parking_brake", "confirmed", "telemetry", "test", default));
                Assert.Equal(0, Store.Get(started.Session.Id).Session.Revision);
            }
            else
            {
                await tools.Update(started.Session.Id, 0, "parking_brake", "confirmed", "telemetry", "test", default);
                var evidence = Store.Get(started.Session.Id).Session.Entries[0].Evidence!;
                Assert.Equal("telemetry", evidence.Source);
                Assert.NotNull(evidence.MonitorId);
                Assert.Equal(sample.CapturedAtUtc, evidence.SampleAtUtc);
                Assert.Equal(1d, evidence.Value);
            }
        }
    }

    private sealed class Session(A320Copilot.Domain.AircraftState sample) : ISimConnectSession
    {
        public void RequestSample() { }
        public A320Copilot.Domain.AircraftState? ReadNext() => sample;
        public void Dispose() { }
    }

    [Theory]
    [InlineData("unvalidated")]
    [InlineData("unavailable")]
    [InlineData("stale")]
    public async Task UnknownQualityCannotConfirmEvenWhenNumericValueMatches(string quality)
    {
        var sample = new A320Copilot.Domain.AircraftState(DateTimeOffset.UtcNow, "FlyByWire A320", 0, 0, 0, true)
        {
            Systems = new("test", new Dictionary<string, A320Copilot.Domain.AircraftParameter>(), new Dictionary<string, A320Copilot.Domain.AircraftParameter>())
            {
                Controls = new Dictionary<string, A320Copilot.Domain.AircraftParameter>
                { ["ParkingBrakeLever"] = new(1, "bool", "L:A32NX_PARK_BRAKE_LEVER_POS") { Quality = quality } }
            }
        };
        using var monitor = new SimConnectAircraftStateSource(new(), () => new Session(sample));
        var tools = new ChecklistTools(Store, new TelemetryReader(new() { Mode = "Real" }, monitor));
        var started = Store.Start("preparation", "test");
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() => tools.Update(started.Session.Id, 0, "parking_brake", "confirmed", "telemetry", "test", default));
        Assert.Equal(0, Store.Get(started.Session.Id).Session.Revision);
        await tools.Update(started.Session.Id, 0, "parking_brake", "confirmed", "user", "User explicitly confirmed visually", default);
        Assert.Equal("user", Store.Get(started.Session.Id).Session.Entries[0].Evidence!.Source);
    }

    [Fact]
    public void RejectsPathTraversalAndEmptyEvidence()
    {
        Assert.Throws<ArgumentException>(() => Store.Get("../outside"));
        var initial = Store.Start("pushback", "test");
        Assert.Throws<ArgumentException>(() => Store.Update(initial.Session.Id, 0, "doors_equipment", "confirmed", User("")));
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}
