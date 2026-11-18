using System.Collections.Concurrent;
using Omni.Core.Diagnostics;
using Omni.Core.Execution;

namespace Omni.Core.DevRunner;

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
    private const int MaxBufferLines = 50;

    public void RegisterService(ServiceSpec spec)
    {
        _services.Add(spec);
        _serviceBuffers[spec.Name] = new List<string>();
    }

    public async Task RunAllAsync(
        Action<string, string, bool> logCallback,
        Action<FailureDiagnosis>? diagnosisCallback = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = _services.Select(svc => Task.Run(async () =>
        {
            logCallback(svc.Name, $"Starting: '{svc.RawCommand}' in {svc.WorkingDirectory}", false);

            var exitCode = await ProcessRunner.RunShellCommandAsync(
                svc.RawCommand,
                svc.WorkingDirectory,
                output =>
                {
                    // Maintain line buffer for diagnostics
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
