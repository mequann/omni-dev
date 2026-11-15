using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Omni.Core.Execution;

public record ProcessOutputEventArgs(string Line, bool IsError, DateTime Timestamp);

public class ProcessRunner
{
    public static async Task<int> RunAsync(
        string command,
        string arguments,
        string workingDirectory,
        Action<ProcessOutputEventArgs>? onOutput = null,
        CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                onOutput?.Invoke(new ProcessOutputEventArgs(e.Data, false, DateTime.UtcNow));
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                onOutput?.Invoke(new ProcessOutputEventArgs(e.Data, true, DateTime.UtcNow));
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        KillProcessTree(process.Id);
                    }
                }
                catch { }
            });

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            return -1;
        }
    }

    private static void KillProcessTree(int processId)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var killer = Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {processId} /T /F",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                killer?.WaitForExit(2000);
            }
            catch { }
        }
        else
        {
            try
            {
                using var proc = Process.GetProcessById(processId);
                proc.Kill(entireProcessTree: true);
            }
            catch { }
        }
    }
}
