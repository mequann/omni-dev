using Omni.Core.Execution;

namespace Omni.Core.DevRunner;

public record ServiceSpec(
    string Name,
    string RawCommand,
    string WorkingDirectory,
    string Color = "cyan",
    Dictionary<string, string>? Env = null
);

public class ConcurrentManager
{
    private readonly List<ServiceSpec> _services = new();

    public void RegisterService(ServiceSpec spec)
    {
        _services.Add(spec);
    }

    public async Task RunAllAsync(
        Action<string, string, bool> logCallback,
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
                    logCallback(svc.Name, output.Line, output.IsError);
                },
                svc.Env,
                cancellationToken
            );

            logCallback(svc.Name, $"Exited with code {exitCode}", exitCode != 0);
        }, cancellationToken)).ToArray();

        await Task.WhenAll(tasks);
    }
}
