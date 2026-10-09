using System.Runtime.InteropServices;
using System.Collections.Concurrent;
using A320Copilot.Domain;

namespace A320Copilot.Bridge;

/// <summary>Minimal native SDK binding. No aircraft write/event functions are imported.</summary>
public sealed class NativeSimConnectSession : ISimConnectSession
{
    private const uint DefinitionId = 1, RequestId = 1;
    // Keep the module resident until process exit; native SDK background code may
    // outlive Close. Never unload it between reconnects or MCP calls.
    private static readonly ConcurrentDictionary<string, Lazy<nint>> Libraries = new(StringComparer.OrdinalIgnoreCase);
    private readonly nint library;
    private nint handle;
    private readonly Close close;
    private readonly AddDefinition addDefinition;
    private readonly RequestData requestData;
    private readonly NextDispatch nextDispatch;

    public NativeSimConnectSession(SimConnectSettings settings)
    {
        settings.Validate();
        if (!OperatingSystem.IsWindows() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("SimConnect requires Windows x64.");
        var path = string.IsNullOrEmpty(settings.LibraryPath)
            ? Path.Combine(AppContext.BaseDirectory, "SimConnect.dll") : settings.LibraryPath;
        if (!File.Exists(path))
            throw new IOException("Official x64 SimConnect.dll was not found. Set SimConnect:LibraryPath or place it beside the application.");
        try
        {
            library = Libraries.GetOrAdd(Path.GetFullPath(path), p => new Lazy<nint>(() => NativeLibrary.Load(p))).Value;
            close = Bind<Close>("SimConnect_Close");
            addDefinition = Bind<AddDefinition>("SimConnect_AddToDataDefinition");
            requestData = Bind<RequestData>("SimConnect_RequestDataOnSimObject");
            nextDispatch = Bind<NextDispatch>("SimConnect_GetNextDispatch");
            Check(Bind<Open>("SimConnect_Open")(out handle, "A320 Copilot Read Only", 0, 0, 0, 0), "open connection; check MSFS is running");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    private T Bind<T>(string name) where T : Delegate
        => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));

    public void RequestSample()
    {
        // SDK datatype values: STRING256=9, FLOAT64=4, INT32=1. User object=0, SECOND=4.
        Add("TITLE", null, 9);
        Add("PLANE ALTITUDE", "feet", 4);
        Add("AIRSPEED INDICATED", "knots", 4);
        Add("PLANE HEADING DEGREES TRUE", "degrees", 4);
        Add("SIM ON GROUND", "Bool", 1);
        foreach (var parameter in FlyByWireParameters.All) Add(parameter.SimVar, parameter.SdkUnit, 4);
        Check(requestData(handle, RequestId, DefinitionId, 0, 4, 0, 0, 0, 0), "subscribe to aircraft data");
    }

    private void Add(string name, string? units, uint type)
        => Check(addDefinition(handle, DefinitionId, name, units, type, 0, uint.MaxValue), $"define {name}");

    public AircraftState? ReadNext()
    {
        var result = nextDispatch(handle, out var data, out var size);
        // The SDK uses E_FAIL when the receive queue has no messages.
        if (result == unchecked((int)0x80004005)) return null;
        Check(result, "receive data");
        if (data == 0 || size < 12) throw new IOException("Invalid SimConnect message header.");
        var declaredSize = unchecked((uint)Marshal.ReadInt32(data));
        if (declaredSize < 12 || declaredSize > size) throw new IOException("Truncated SimConnect message.");
        var id = Marshal.ReadInt32(data, 8);
        if (id == 3) throw new IOException("MSFS closed the SimConnect session."); // QUIT
        if (id == 1) // EXCEPTION
        {
            if (declaredSize < 24) throw new IOException("Truncated SimConnect exception.");
            throw new IOException($"SimConnect exception {Marshal.ReadInt32(data, 12)} (send {Marshal.ReadInt32(data, 16)}, index {Marshal.ReadInt32(data, 20)}).");
        }
        if (id != 8) return null; // SIMOBJECT_DATA
        if (declaredSize < 40) throw new IOException("Truncated SimConnect data header.");
        if (Marshal.ReadInt32(data, 12) != RequestId || Marshal.ReadInt32(data, 20) != DefinitionId)
            return null;
        // ObjectID in the response is the actual runtime ID, not the USER=0 request alias.
        if (Marshal.ReadInt32(data, 24) != 0 || Marshal.ReadInt32(data, 36) != 5 + FlyByWireParameters.All.Count)
            throw new IOException($"Unexpected SimConnect flags {Marshal.ReadInt32(data, 24)} or field count {Marshal.ReadInt32(data, 36)}.");
        if (declaredSize < 40 + SimConnectSampleParser.PayloadSize)
            throw new IOException("Truncated SimConnect aircraft payload.");
        var payload = new byte[SimConnectSampleParser.PayloadSize];
        Marshal.Copy(data + 40, payload, 0, payload.Length);
        return SimConnectSampleParser.Parse(payload, DateTimeOffset.UtcNow);
    }

    private static void Check(int result, string operation)
    {
        if (result < 0) throw new IOException($"SimConnect could not {operation} (HRESULT 0x{result:X8}).");
    }

    public void Dispose()
    {
        if (handle != 0) { close(handle); handle = 0; }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate int Open(out nint handle, string name, nint window, uint message, nint eventHandle, uint config);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int Close(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate int AddDefinition(nint handle, uint definition, string name, string? units, uint type, float epsilon, uint datum);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int RequestData(nint handle, uint request, uint definition, uint objectId, uint period, uint flags, uint origin, uint interval, uint limit);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int NextDispatch(nint handle, out nint data, out uint size);
}
