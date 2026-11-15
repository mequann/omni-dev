using Omni.Core.Execution;

namespace Omni.Core.DevRunner;

public record ServiceSpec(
    string Name,
    string Command,
    string Arguments,
    string WorkingDirectory,
    string ColorHex = "Cyan"
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
            logCallback(svc.Name, $"Starting '{svc.Command} {svc.Arguments}'...", false);

            var exitCode = await ProcessRunner.RunAsync(
                svc.Command,
                svc.Arguments,
                svc.WorkingDirectory,
                output =>
                {
                    logCallback(svc.Name, output.Line, output.IsError);
                },
                cancellationToken
            );

            logCallback(svc.Name, $"Exited with code {exitCode}", exitCode != 0);
        }, cancellationToken)).ToArray();

        await Task.WhenAll(tasks);
    }
}
