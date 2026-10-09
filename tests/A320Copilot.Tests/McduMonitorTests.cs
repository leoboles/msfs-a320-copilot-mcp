using System.Text.Json;
using A320Copilot.Bridge;
using Xunit;

namespace A320Copilot.Tests;

public sealed class McduMonitorTests
{
    private static JsonElement Screen(string title = "INIT", string scratchpad = "", string line = "FROM/TO", double brightness = 0.5)
        => JsonSerializer.SerializeToElement(new { title, scratchpad, lines = new[] { new[] { line, "", "" } }, displayBrightness = brightness });

    [Fact]
    public void FreshnessUsesMonotonicTimeAndNeverRevivesOldSessionScreen()
    {
        var clock = new ManualClock();
        var store = new McduMonitorStore(new(), clock);
        store.BeginConnection(); store.SocketConnected();
        Assert.Equal("unavailable", store.GetStatus().Validity);
        store.Publish(Screen());
        Assert.Equal("fresh", store.GetFreshSnapshot()!.Freshness!.Validity);
        clock.Utc = clock.Utc.AddHours(-5);
        clock.Advance(3001);
        Assert.Null(store.GetFreshSnapshot());
        Assert.Equal(3001, store.GetStatus().AgeMilliseconds);
        Assert.Single(store.GetEvents().Events, e => e.Type == "data_stale");
        store.Publish(Screen());
        Assert.Single(store.GetEvents().Events, e => e.Type == "data_fresh");
        store.Disconnected("lost");
        Assert.Null(store.GetFreshSnapshot());
        store.BeginConnection(); store.SocketConnected();
        Assert.Equal("unavailable", store.GetStatus().Validity);
        Assert.Null(store.GetFreshSnapshot());
        store.Publish(Screen(title: "FUEL PRED"));
        Assert.Equal(2, store.GetStatus().SessionId);
        Assert.DoesNotContain(store.GetEvents().Events, e => e.Type == "page_changed");
    }

    [Fact]
    public void ChangesContainOnlyChangedFieldsAndSuppressDuplicateScreensAndBrightnessJitter()
    {
        var store = new McduMonitorStore(new());
        store.BeginConnection(); store.SocketConnected(); store.Publish(Screen());
        var cursor = store.GetEvents().NextAfterSequence;
        store.Publish(Screen()); store.Publish(Screen(brightness: 0.505));
        Assert.Empty(store.GetEvents(cursor).Events);
        store.Publish(Screen("FUEL PRED", "CHECK WEIGHT", "ZFW", 0.52));
        var changes = store.GetEvents(cursor).Events;
        Assert.Equal(4, changes.Count);
        Assert.Single(changes, e => e.Type == "page_changed" && e.Field == "title");
        var line = Assert.Single(changes, e => e.Field == "lines[0]");
        Assert.Equal("ZFW", ((JsonElement)line.Value!)[0].GetString());
        Assert.Single(changes, e => e.Field == "scratchpad");
        Assert.Single(changes, e => e.Field == "displayBrightness");
        Assert.Empty(store.GetEvents(changes[^1].Sequence).Events);
    }

    [Fact]
    public void JournalIsBoundedAndCursorsDoNotConsumeOtherClientsEvents()
    {
        var store = new McduMonitorStore(new() { EventCapacity = 10 });
        store.BeginConnection(); store.SocketConnected(); store.Publish(Screen());
        for (var i = 0; i < 30; i++) store.Publish(Screen(scratchpad: i.ToString()));
        var first = store.GetEvents(0, 2);
        Assert.True(first.HistoryTruncated);
        Assert.True(first.HasMore);
        Assert.Equal(first.Events, store.GetEvents(0, 2).Events);
        var next = store.GetEvents(first.NextAfterSequence, 200);
        Assert.False(next.HasMore);
        Assert.Empty(store.GetEvents(next.NextAfterSequence).Events);
        Assert.Throws<ArgumentException>(() => store.GetEvents(long.MaxValue));
        Assert.Throws<ArgumentException>(() => store.GetEvents(-1));
        Assert.Throws<ArgumentException>(() => store.GetEvents(0, 201));
    }

    private sealed class ManualClock : TimeProvider
    {
        private long timestamp;
        public DateTimeOffset Utc { get; set; } = DateTimeOffset.UtcNow;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => timestamp;
        public override DateTimeOffset GetUtcNow() => Utc;
        public void Advance(int milliseconds) { timestamp += milliseconds; Utc = Utc.AddMilliseconds(milliseconds); }
    }
}
