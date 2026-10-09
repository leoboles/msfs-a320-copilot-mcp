using System.Buffers.Binary;
using System.Text;
using A320Copilot.Domain;

namespace A320Copilot.Bridge;

public static class SimConnectSampleParser
{
    // SDK data is packed, with no padding: STRING256, 3 FLOAT64, INT32.
    public const int BasicPayloadSize = 284;
    public static int PayloadSize => BasicPayloadSize + FlyByWireParameters.All.Count * sizeof(double);

    public static AircraftState Parse(ReadOnlySpan<byte> payload, DateTimeOffset receivedAt)
    {
        if (payload.Length < PayloadSize) throw new IOException("Truncated SimConnect aircraft sample.");
        var titleBytes = payload[..256];
        var terminator = titleBytes.IndexOf((byte)0);
        if (terminator < 0) throw new IOException("Invalid SimConnect aircraft title.");
        var title = Encoding.UTF8.GetString(titleBytes[..terminator]);
        var altitude = BinaryPrimitives.ReadDoubleLittleEndian(payload[256..]);
        var airspeed = BinaryPrimitives.ReadDoubleLittleEndian(payload[264..]);
        var heading = BinaryPrimitives.ReadDoubleLittleEndian(payload[272..]);
        if (string.IsNullOrWhiteSpace(title) || !double.IsFinite(altitude) ||
            !double.IsFinite(airspeed) || !double.IsFinite(heading))
            throw new IOException("SimConnect returned an invalid aircraft sample.");
        var overhead = new Dictionary<string, AircraftParameter>();
        var engines = new Dictionary<string, AircraftParameter>();
        var fuel = new Dictionary<string, AircraftParameter>();
        var controls = new Dictionary<string, AircraftParameter>();
        var compatible = AircraftCompatibility.IsA32Nx(title);
        for (var index = 0; index < FlyByWireParameters.All.Count; index++)
        {
            var parameter = FlyByWireParameters.All[index];
            var value = BinaryPrimitives.ReadDoubleLittleEndian(payload[(BasicPayloadSize + index * sizeof(double))..]);
            var available = compatible && double.IsFinite(value);
            (parameter.Group switch { "Overhead" => overhead, "Engines" => engines, "Fuel" => fuel, "Controls" => controls,
                _ => throw new IOException("Unknown telemetry group.") }).Add(parameter.Name,
                new AircraftParameter(available ? value : null, parameter.Unit, parameter.SimVar)
                {
                    Quality = available ? FieldQuality.Unvalidated : FieldQuality.Unavailable,
                    QualityReason = !compatible ? "Aircraft title does not identify a supported FlyByWire A320/A32NX."
                        : !available ? "SimConnect returned a non-finite value."
                        : "Numeric value reported; variable existence and cockpit positions are not validated for the installed aircraft version."
                });
        }
        return new AircraftState(receivedAt.ToUniversalTime(), title, altitude, airspeed,
            (heading % 360 + 360) % 360, BinaryPrimitives.ReadInt32LittleEndian(payload[280..]) != 0)
        {
            // Successful numeric reads do not prove LVAR existence or correctness on every aircraft/version.
            Systems = new AircraftSystemsState(compatible ? "reported_values_not_cross_checked_with_cockpit"
                : "unsupported_aircraft", overhead, engines) { Fuel = fuel, Controls = controls }
        };
    }
}
