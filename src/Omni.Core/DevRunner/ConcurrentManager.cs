using System.Collections.Concurrent;
using Omni.Core.Diagnostics;
using Omni.Core.Execution;

namespace Omni.Core.DevRunner;

public enum ServiceState
{
    Starting,
    Running,
    ExitedOk,
    Failed
}

public record ServiceSpec(
    string Name,
    string RawCommand,
    string WorkingDirectory,
    string Color = "cyan",
    Dictionary<string, string>? Env = null,
    int? Port = null
);

public class ConcurrentManager
{
    private readonly List<ServiceSpec> _services = new();
    public IReadOnlyList<ServiceSpec> Services => _services;
    private readonly ConcurrentDictionary<string, List<string>> _serviceBuffers = new();
    private readonly ConcurrentDictionary<string, ServiceState> _serviceStates = new();
    private readonly ConcurrentDictionary<string, int> _exitCodes = new();
    private const int MaxBufferLines = 60;

    public void RegisterService(ServiceSpec spec)
    {
        _services.Add(spec);
        _serviceBuffers[spec.Name] = new List<string>();
        _serviceStates[spec.Name] = ServiceState.Starting;
    }

    public ServiceState GetServiceState(string serviceName) =>
        _serviceStates.TryGetValue(serviceName, out var s) ? s : ServiceState.Starting;

    public int? GetExitCode(string serviceName) =>
        _exitCodes.TryGetValue(serviceName, out var c) ? c : null;

    public IReadOnlyList<string> GetServiceBuffer(string serviceName)
    {
        if (_serviceBuffers.TryGetValue(serviceName, out var buffer))
        {
            lock (buffer)
            {
                return buffer.ToList();
            }
        }
        return Array.Empty<string>();
    }

    public void ClearServiceBuffer(string serviceName)
    {
        if (_serviceBuffers.TryGetValue(serviceName, out var buffer))
        {
            lock (buffer)
            {
                buffer.Clear();
            }
        }
    }

    public async Task RunAllAsync(
        Action<string, string, bool> logCallback,
        Action<FailureDiagnosis>? diagnosisCallback = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = _services.Select(svc => Task.Run(async () =>
        {
            _serviceStates[svc.Name] = ServiceState.Starting;
            logCallback(svc.Name, $"Starting: '{svc.RawCommand}' in {svc.WorkingDirectory}", false);

            var exitCode = await ProcessRunner.RunShellCommandAsync(
                svc.RawCommand,
                svc.WorkingDirectory,
                output =>
                {
                    _serviceStates[svc.Name] = ServiceState.Running;

                    // Maintain line buffer for diagnostics and TUI
                    if (_serviceBuffers.TryGetValue(svc.Name, out var buffer))
                    {
                        lock (buffer)
                        {
                            if (buffer.Count >= MaxBufferLines)
                            {
                                buffer.RemoveAt(0);
                            }
                            buffer.Add(output.Line);
                        }
                    }

                    logCallback(svc.Name, output.Line, output.IsError);
                },
                svc.Env,
                cancellationToken
            );

            _exitCodes[svc.Name] = exitCode;
            _serviceStates[svc.Name] = exitCode == 0 ? ServiceState.ExitedOk : ServiceState.Failed;
            logCallback(svc.Name, $"Exited with code {exitCode}", exitCode != 0);

            // If exited with non-zero and not cancelled, run failure diagnosis
            if (exitCode != 0 && !cancellationToken.IsCancellationRequested)
            {
                List<string> snapshotLines;
                if (_serviceBuffers.TryGetValue(svc.Name, out var buffer))
                {
                    lock (buffer)
                    {
                        snapshotLines = buffer.ToList();
                    }
                }
                else
                {
                    snapshotLines = new List<string>();
                }

                var diagnosis = FailureAnalyzer.Analyze(svc.Name, exitCode, snapshotLines);
                diagnosisCallback?.Invoke(diagnosis);
            }
        }, cancellationToken)).ToArray();

        await Task.WhenAll(tasks);
    }
}
