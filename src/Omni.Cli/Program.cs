using Spectre.Console;
using Omni.Core.Detection;
using Omni.Core.DevRunner;

namespace Omni.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("Omni")
                .Color(Color.Cyan1));

        AnsiConsole.MarkupLine("[bold grey]Universal Developer Orchestrator & Concurrent Dev Runner[/]\n");

        var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

        return command switch
        {
            "analyze" => RunAnalyze(),
            "dev" => await RunDevAsync(),
            _ => ShowHelp()
        };
    }

    private static int RunAnalyze()
    {
        var detector = new WorkspaceDetector();
        var currentDir = Directory.GetCurrentDirectory();

        AnsiConsole.MarkupLine($"[bold]Scanning workspace:[/] [yellow]{currentDir}[/]\n");

        var projects = detector.Detect(currentDir);

        if (!projects.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No supported toolchains detected automatically.[/]");
            return 0;
        }

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold]Component[/]");
        table.AddColumn("[bold]Toolchain[/]");
        table.AddColumn("[bold]Path[/]");
        table.AddColumn("[bold]Dev Command[/]");

        foreach (var p in projects)
        {
            var badgeColor = p.Kind switch
            {
                ToolchainKind.DotNet => "purple",
                ToolchainKind.Node => "green",
                ToolchainKind.Python => "blue",
                ToolchainKind.DockerCompose => "cyan",
                _ => "grey"
            };

            table.AddRow(
                $"[bold]{p.Name}[/]",
                $"[{badgeColor}]{p.Kind}[/]",
                $"[grey]{Path.GetRelativePath(currentDir, p.Path)}[/]",
                $"[yellow]{p.RecommendedDevCommand} {p.RecommendedDevArgs}[/]"
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[bold green]💡 Orchestration Insights:[/] ");
        AnsiConsole.MarkupLine(" • [white]Independent services detected. Run concurrently with:[/] [cyan]omni dev[/]");
        AnsiConsole.MarkupLine(" • [white]To run build tasks across all services:[/] [cyan]omni run build[/]");

        return 0;
    }

    private static async Task<int> RunDevAsync()
    {
        var detector = new WorkspaceDetector();
        var currentDir = Directory.GetCurrentDirectory();
        var projects = detector.Detect(currentDir);

        if (!projects.Any())
        {
            AnsiConsole.MarkupLine("[red]Error:[/] No services found to run. Create a project or configuration first.");
            return 1;
        }

        var colors = new[] { "cyan", "green", "magenta", "yellow", "blue" };
        var manager = new ConcurrentManager();

        for (int i = 0; i < projects.Count; i++)
        {
            var p = projects[i];
            var color = colors[i % colors.Length];
            manager.RegisterService(new ServiceSpec(
                p.Name,
                p.RecommendedDevCommand,
                p.RecommendedDevArgs,
                p.Path,
                color
            ));
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            AnsiConsole.MarkupLine("\n[bold red]Stopping all services gracefully...[/]");
            cts.Cancel();
        };

        AnsiConsole.MarkupLine($"[bold]Starting [cyan]{projects.Count}[/] services in parallel... (Press Ctrl+C to stop)[/]\n");

        var badgeMap = projects.ToDictionary(
            p => p.Name,
            p => colors[projects.IndexOf(p) % colors.Length]
        );

        await manager.RunAllAsync((serviceName, line, isError) =>
        {
            var color = badgeMap.TryGetValue(serviceName, out var c) ? c : "white";
            var prefix = $"[{color}][[{serviceName}]][/]";
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

    private static int ShowHelp()
    {
        AnsiConsole.MarkupLine("[bold]Usage:[/] omni <command>\n");
        AnsiConsole.MarkupLine("[bold]Commands:[/] ");
        AnsiConsole.MarkupLine("  [cyan]analyze[/]    Inspect repository, detect toolchains, and report bottlenecks");
        AnsiConsole.MarkupLine("  [cyan]dev[/]        Concurrently run microservices, frontend, and backend");
        AnsiConsole.MarkupLine("  [cyan]run <task>[/]  Execute workflow tasks with dependency resolution");
        AnsiConsole.MarkupLine("  [cyan]explain[/]    Diagnose why a build step ran or skipped");
        return 0;
    }
}
