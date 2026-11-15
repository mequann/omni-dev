using Spectre.Console;
using Omni.Core.Config;
using Omni.Core.Detection;
using Omni.Core.DevRunner;
using Omni.Core.Execution;

namespace Omni.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("Omni")
                .Color(Color.Cyan1));

        AnsiConsole.MarkupLine("[bold grey]Universal Developer Orchestrator & Concurrent Dev Runner[/]\n");

        if (args.Length == 0)
        {
            return ShowHelp();
        }

        var command = args[0].ToLowerInvariant();

        return command switch
        {
            "init" => RunInit(),
            "analyze" => RunAnalyze(),
            "dev" => await RunDevAsync(),
            "run" => await RunTaskAsync(args.Length > 1 ? args[1] : null),
            _ => ShowHelp()
        };
    }

    private static int RunInit()
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), "omni.yaml");
        if (File.Exists(configPath))
        {
            AnsiConsole.MarkupLine("[yellow]omni.yaml already exists in this directory.[/]");
            return 0;
        }

        File.WriteAllText(configPath, OmniConfig.GenerateSampleYaml());
        AnsiConsole.MarkupLine("[green]✔ Created omni.yaml[/] - Customize services, ports, and commands for your stack.");
        return 0;
    }

    private static int RunAnalyze()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "omni.yaml");
        var config = OmniConfig.LoadFromFile(configPath);

        if (config != null)
        {
            AnsiConsole.MarkupLine($"[bold]Loaded Configuration:[/] [cyan]omni.yaml[/] ([yellow]{config.Name}[/])\n");

            var svcTable = new Table().Border(TableBorder.Rounded);
            svcTable.AddColumn("[bold]Service[/]");
            svcTable.AddColumn("[bold]Command[/]");
            svcTable.AddColumn("[bold]Directory[/]");
            svcTable.AddColumn("[bold]Port[/]");

            foreach (var (name, svc) in config.Services)
            {
                svcTable.AddRow(
                    $"[bold {svc.Color ?? "cyan"}]{name}[/]",
                    $"[grey]{Markup.Escape(svc.Command)}[/]",
                    $"[white]{svc.Dir ?? "."}[/]",
                    $"[yellow]{svc.Port?.ToString() ?? "-"}[/]"
                );
            }
            AnsiConsole.Write(svcTable);
            return 0;
        }

        AnsiConsole.MarkupLine($"[bold]Scanning workspace:[/] [yellow]{currentDir}[/]\n");
        var detector = new WorkspaceDetector();
        var projects = detector.Detect(currentDir);

        if (!projects.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No supported toolchains detected automatically.[/]");
            AnsiConsole.MarkupLine("Run [cyan]omni init[/] to create a custom configuration.");
            return 0;
        }

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold]Component[/]");
        table.AddColumn("[bold]Technology[/]");
        table.AddColumn("[bold]Path[/]");
        table.AddColumn("[bold]Dev Command[/]");

        foreach (var p in projects)
        {
            var badgeColor = p.Kind switch
            {
                ToolchainKind.DotNet => "purple",
                ToolchainKind.Node => "green",
                ToolchainKind.Python => "blue",
                ToolchainKind.Rust => "red",
                ToolchainKind.Go => "cyan",
                ToolchainKind.Java => "orange1",
                ToolchainKind.Make or ToolchainKind.CMake => "grey",
                ToolchainKind.DockerCompose => "deepskyblue1",
                _ => "white"
            };

            table.AddRow(
                $"[bold]{p.Name}[/]",
                $"[{badgeColor}]{p.Kind}[/]",
                $"[grey]{Path.GetRelativePath(currentDir, p.Path)}[/]",
                $"[yellow]{Markup.Escape(p.RecommendedDevCommand)}[/]"
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[bold green]💡 Orchestration Insights:[/] ");
        AnsiConsole.MarkupLine(" • [white]Run all services concurrently:[/] [cyan]omni dev[/]");
        AnsiConsole.MarkupLine(" • [white]Lock or customize your configuration:[/] [cyan]omni init[/]");

        return 0;
    }

    private static async Task<int> RunDevAsync()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "omni.yaml");
        var config = OmniConfig.LoadFromFile(configPath);
        var manager = new ConcurrentManager();

        var colors = new[] { "cyan", "green", "magenta", "yellow", "blue", "red", "orange1" };

        if (config != null && config.Services.Any())
        {
            int idx = 0;
            foreach (var (name, svc) in config.Services)
            {
                var workDir = string.IsNullOrEmpty(svc.Dir) 
                    ? currentDir 
                    : Path.GetFullPath(Path.Combine(currentDir, svc.Dir));

                manager.RegisterService(new ServiceSpec(
                    name,
                    svc.Command,
                    workDir,
                    svc.Color ?? colors[idx % colors.Length],
                    svc.Env
                ));
                idx++;
            }
        }
        else
        {
            var detector = new WorkspaceDetector();
            var projects = detector.Detect(currentDir);

            if (!projects.Any())
            {
                AnsiConsole.MarkupLine("[red]Error:[/] No services found. Run [cyan]omni init[/] to configure your stack.");
                return 1;
            }

            for (int i = 0; i < projects.Count; i++)
            {
                var p = projects[i];
                manager.RegisterService(new ServiceSpec(
                    p.Name,
                    p.RecommendedDevCommand,
                    p.Path,
                    colors[i % colors.Length]
                ));
            }
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            AnsiConsole.MarkupLine("\n[bold red]Stopping all services gracefully...[/]");
            cts.Cancel();
        };

        AnsiConsole.MarkupLine("[bold]Starting services in parallel... (Press [red]Ctrl+C[/] to stop)[/]\n");

        await manager.RunAllAsync((serviceName, line, isError) =>
        {
            var prefix = $"[bold][[ {serviceName} ]][/]";
            if (isError)
            {
                AnsiConsole.MarkupLine($"{prefix} [red]{Markup.Escape(line)}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"{prefix} [grey]{Markup.Escape(line)}[/]");
            }
        }, cts.Token);

        return 0;
    }

    private static async Task<int> RunTaskAsync(string? taskName)
    {
        if (string.IsNullOrEmpty(taskName))
        {
            AnsiConsole.MarkupLine("[red]Please specify a task to run (e.g., omni run test, omni run build)[/]");
            return 1;
        }

        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "omni.yaml");
        var config = OmniConfig.LoadFromFile(configPath);

        if (config == null || !config.Tasks.TryGetValue(taskName, out var taskDef))
        {
            AnsiConsole.MarkupLine($"[yellow]No custom task '{taskName}' found in omni.yaml. Falling back to auto-detection...[/]");
            var detector = new WorkspaceDetector();
            var projects = detector.Detect(currentDir);

            foreach (var p in projects)
            {
                var cmd = taskName == "build" ? p.RecommendedBuildCommand : $"{p.RecommendedDevCommand}";
                AnsiConsole.MarkupLine($"[bold cyan]Running on {p.Name}:[/] [yellow]{cmd}[/]");
                var code = await ProcessRunner.RunShellCommandAsync(cmd, p.Path, outLine =>
                {
                    Console.WriteLine(outLine.Line);
                });

                if (code != 0) return code;
            }
            return 0;
        }

        AnsiConsole.MarkupLine($"[bold cyan]Executing task:[/] [yellow]{taskName}[/] (Parallel: {taskDef.Parallel})\n");

        if (taskDef.Parallel)
        {
            var running = taskDef.Commands.Select(cmd => Task.Run(async () =>
            {
                AnsiConsole.MarkupLine($"[grey]Starting:[/] {cmd}");
                return await ProcessRunner.RunShellCommandAsync(cmd, currentDir, outLine =>
                {
                    Console.WriteLine($"[{cmd}] {outLine.Line}");
                });
            })).ToArray();

            var results = await Task.WhenAll(running);
            return results.Any(r => r != 0) ? 1 : 0;
        }
        else
        {
            foreach (var cmd in taskDef.Commands)
            {
                AnsiConsole.MarkupLine($"[bold]Executing:[/] [yellow]{cmd}[/]");
                var code = await ProcessRunner.RunShellCommandAsync(cmd, currentDir, outLine =>
                {
                    Console.WriteLine(outLine.Line);
                });
                if (code != 0) return code;
            }
        }

        AnsiConsole.MarkupLine("\n[bold green]✔ Task completed successfully![/]");
        return 0;
    }

    private static int ShowHelp()
    {
        AnsiConsole.MarkupLine("[bold]Usage:[/] omni <command>\n");
        AnsiConsole.MarkupLine("[bold]Universal Commands:[/] ");
        AnsiConsole.MarkupLine("  [cyan]init[/]          Create an omni.yaml configuration for your stack");
        AnsiConsole.MarkupLine("  [cyan]analyze[/]       Inspect workspace, detect toolchains, and report layout");
        AnsiConsole.MarkupLine("  [cyan]dev[/]           Concurrently run all services (frontend, backend, workers, DB)");
        AnsiConsole.MarkupLine("  [cyan]run <task>[/]     Execute pipeline tasks (build, test, deploy) with parallel support");
        return 0;
    }
}
