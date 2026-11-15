using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace Omni.Core.Diagnostics;

public record PortStatus(int Port, bool IsInUse, string? Note = null);
public record ToolchainStatus(string Name, bool IsInstalled, string? Version = null);

public class PreflightChecker
{
    public static bool IsPortInUse(int port)
    {
        if (port <= 0 || port > 65535)
            return false;

        try
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpListeners = ipGlobalProperties.GetActiveTcpListeners();
            return tcpListeners.Any(endpoint => endpoint.Port == port);
        }
        catch
        {
            return false;
        }
    }

    public static PortStatus CheckPort(int port)
    {
        var inUse = IsPortInUse(port);
        return new PortStatus(
            port,
            inUse,
            inUse ? $"Port {port} is currently bound by another running process." : "Available"
        );
    }

    public static async Task<ToolchainStatus> CheckToolAsync(string toolName, string versionFlag = "--version")
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/sh",
                Arguments = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? $"/c {toolName} {versionFlag}"
                    : $"-c \"{toolName} {versionFlag}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var waitResult = await Task.WhenAny(proc.WaitForExitAsync(), Task.Delay(3000));

            if (proc.HasExited && proc.ExitCode == 0)
            {
                var stdout = (await stdoutTask).Trim();
                var firstLine = stdout.Split('\n').FirstOrDefault()?.Trim() ?? "Installed";
                return new ToolchainStatus(toolName, true, firstLine);
            }

            return new ToolchainStatus(toolName, false);
        }
        catch
        {
            return new ToolchainStatus(toolName, false);
        }
    }

    public static async Task<List<ToolchainStatus>> CheckAllToolchainsAsync()
    {
        var tools = new[]
        {
            ("dotnet", "--version"),
            ("node", "--version"),
            ("npm", "--version"),
            ("python", "--version"),
            ("cargo", "--version"),
            ("go", "version"),
            ("docker", "--version"),
            ("git", "--version")
        };

        var tasks = tools.Select(t => CheckToolAsync(t.Item1, t.Item2));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }
}

