using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

// Windows PowerShell's native pipeline buffers input. Relay the raw streams
// concurrently so a long-lived MCP connection can exchange requests immediately.
public static class A320PluginStdio
{
    private static async Task Pump(Stream source, Stream destination, bool closeDestination)
    {
        try
        {
            var buffer = new byte[8192];
            int count;
            while ((count = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
            {
                await destination.WriteAsync(buffer, 0, count).ConfigureAwait(false);
                await destination.FlushAsync().ConfigureAwait(false);
            }
        }
        catch (IOException) { /* The peer may exit while a pipe is being drained. */ }
        finally { if (closeDestination) destination.Dispose(); }
    }

    public static int Run(string executable)
    {
        using (var process = new Process())
        {
            process.StartInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            process.Start();
            // Console streams on .NET Framework can complete ReadAsync synchronously;
            // start each pump on its own worker to avoid blocking the other directions.
            Task.Run(() => Pump(Console.OpenStandardInput(), process.StandardInput.BaseStream, true));
            var output = Task.Run(() => Pump(process.StandardOutput.BaseStream, Console.OpenStandardOutput(), false));
            var error = Task.Run(() => Pump(process.StandardError.BaseStream, Console.OpenStandardError(), false));
            process.WaitForExit();
            Task.WaitAll(output, error);
            return process.ExitCode;
        }
    }
}
