using System.Diagnostics;
using System.Runtime.InteropServices;
using Spectre.Console;
using Omni.Core.Config;
using Omni.Core.Detection;
using Omni.Core.DevRunner;
using Omni.Core.Diagnostics;
using Omni.Core.Execution;

namespace Omni.Cli;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("Omni")
                .Color(Color.Cyan1));

        AnsiConsole.MarkupLine("[bold grey]Universal Developer Orchestrator & Intelligent Observability Engine[/]\n");

        if (args.Length == 0)
        {
            return ShowHelp();
        }

        var command = args[0].ToLowerInvariant();

        return command switch
        {
            "-h" or "--help" or "help" => ShowHelp(),
            "-v" or "--version" or "version" => ShowVersion(),
            "init" => RunInit(),
            "analyze" => RunAnalyze(),
            "doctor" => await RunDoctorAsync(),
            "dev" => await RunDevAsync(args.Skip(1).ToArray()),
            "open" => RunOpen(),
            "run" => await RunTaskAsync(args.Length > 1 ? args[1] : null),
            _ => ShowHelp(command)
        };
    }

    private static int ShowVersion()
    {
        AnsiConsole.MarkupLine("[bold cyan]Omni[/] version [green]0.2.0-preview[/] (.NET 9 / .NET 10)");
        return 0;
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
            svcTable.AddColumn("[bold]Port Status[/]");

            foreach (var (name, svc) in config.Services)
            {
                string portStatus = "-";
                if (svc.Port.HasValue)
                {
                    var status = PreflightChecker.CheckPort(svc.Port.Value);
                    portStatus = status.IsInUse ? "[red]Busy (Conflict)[/]" : "[green]Available[/]";
                }

                svcTable.AddRow(
                    $"[bold {svc.Color ?? "cyan"}]{name}[/]",
                    $"[grey]{Markup.Escape(svc.Command)}[/]",
                    $"[white]{svc.Dir ?? "."}[/]",
                    $"[yellow]{svc.Port?.ToString() ?? "-"}[/]",
                    portStatus
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
        AnsiConsole.MarkupLine(" • [white]Check system health & ports:[/] [cyan]omni doctor[/]");
        AnsiConsole.MarkupLine(" • [white]Lock or customize your configuration:[/] [cyan]omni init[/]");

        return 0;
    }

    private static async Task<int> RunDoctorAsync()
    {
        AnsiConsole.MarkupLine("[bold cyan]Running System & Workspace Pre-flight Check...[/]\n");

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("[bold]Tool / Runtime[/]");
        table.AddColumn("[bold]Status[/]");
        table.AddColumn("[bold]Detected Version / Notes[/]");

        var statuses = await PreflightChecker.CheckAllToolchainsAsync();

        foreach (var tool in statuses)
        {
            table.AddRow(
                $"[bold]{tool.Name}[/]",
                tool.IsInstalled ? "[green]✔ Installed[/]" : "[red]✘ Not Found[/]",
                tool.IsInstalled ? $"[grey]{Markup.Escape(tool.Version ?? "")}[/]" : "[yellow]Optional / Not in PATH[/]"
            );
        }

        AnsiConsole.Write(table);

        // Check workspace ports
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "omni.yaml");
        var config = OmniConfig.LoadFromFile(configPath);

        if (config != null && config.Services.Any(s => s.Value.Port.HasValue))
        {
            AnsiConsole.MarkupLine("\n[bold]Checking Configured Ports:[/]");
            var portTable = new Table().Border(TableBorder.Rounded);
            portTable.AddColumn("[bold]Service[/]");
            portTable.AddColumn("[bold]Port[/]");
            portTable.AddColumn("[bold]Port State[/]");

            foreach (var (name, svc) in config.Services)
            {
                if (svc.Port.HasValue)
                {
                    var portStatus = PreflightChecker.CheckPort(svc.Port.Value);
                    portTable.AddRow(
                        $"[cyan]{name}[/]",
                        svc.Port.Value.ToString(),
                        portStatus.IsInUse 
                            ? "[bold red]⚠ Port Conflict - In Use[/]" 
                            : "[green]✔ Available[/]"
                    );
                }
            }
            AnsiConsole.Write(portTable);
        }

        return 0;
    }

    private static async Task<int> RunDevAsync(string[]? devArgs = null)
    {
        var argsList = devArgs?.ToList() ?? new List<string>();
        bool openInBrowser = argsList.Contains("--open") || argsList.Contains("-o");
        bool autoKill = argsList.Contains("--kill") || argsList.Contains("-k");

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

                var env = new Dictionary<string, string>(svc.Env ?? new());

                int? effectivePort = svc.Port;
                if (effectivePort.HasValue && PreflightChecker.IsPortInUse(effectivePort.Value))
                {
                    if (autoKill && PreflightChecker.TryKillProcessOnPort(effectivePort.Value))
                    {
                        AnsiConsole.MarkupLine($"[bold yellow]🔪 Auto-killed conflicting process on port [red]{effectivePort.Value}[/] for service '[cyan]{name}[/]'[/]");
                    }
                    else
                    {
                        var freePort = PreflightChecker.FindNextAvailablePort(effectivePort.Value);
                        AnsiConsole.MarkupLine(
                            $"[bold yellow]⚡ Port {effectivePort.Value} for '{name}' is occupied. Automatically remapping to free port [bold green]{freePort}[/]![/]");
                        effectivePort = freePort;
                    }
                }

                if (effectivePort.HasValue)
                {
                    env["PORT"] = effectivePort.Value.ToString();
                    env["ASPNETCORE_URLS"] = $"http://localhost:{effectivePort.Value}";
                }

                manager.RegisterService(new ServiceSpec(
                    name,
                    svc.Command,
                    workDir,
                    svc.Color ?? colors[idx % colors.Length],
                    env,
                    effectivePort
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
                int? defaultPort = p.Kind switch
                {
                    ToolchainKind.Node => 3000 + i,
                    ToolchainKind.DotNet => 5000 + i,
                    ToolchainKind.Python => 8000 + i,
                    _ => null
                };

                var env = new Dictionary<string, string>();
                if (defaultPort.HasValue && PreflightChecker.IsPortInUse(defaultPort.Value))
                {
                    if (autoKill && PreflightChecker.TryKillProcessOnPort(defaultPort.Value))
                    {
                        AnsiConsole.MarkupLine($"[bold yellow]🔪 Auto-killed conflicting process on port [red]{defaultPort.Value}[/] for service '[cyan]{p.Name}[/]'[/]");
                    }
                    else
                    {
                        var freePort = PreflightChecker.FindNextAvailablePort(defaultPort.Value);
                        AnsiConsole.MarkupLine($"[bold yellow]⚡ Port {defaultPort.Value} for '{p.Name}' is occupied. Remapping to [bold green]{freePort}[/]![/]");
                        defaultPort = freePort;
                    }
                }

                if (defaultPort.HasValue)
                {
                    env["PORT"] = defaultPort.Value.ToString();
                    env["ASPNETCORE_URLS"] = $"http://localhost:{defaultPort.Value}";
                }

                manager.RegisterService(new ServiceSpec(
                    p.Name,
                    p.RecommendedDevCommand,
                    p.Path,
                    colors[i % colors.Length],
                    env,
                    defaultPort
                ));
            }
        }

        // Render Service URLs Table Banner
        var urlTable = new Table().Border(TableBorder.Rounded);
        urlTable.AddColumn("[bold]Service[/]");
        urlTable.AddColumn("[bold]Local URL[/]");
        urlTable.AddColumn("[bold]Directory[/]");
        urlTable.AddColumn("[bold]Dev Command[/]");

        foreach (var svc in manager.Services)
        {
            string urlStr = svc.Port.HasValue ? $"http://localhost:{svc.Port.Value}" : "-";
            urlTable.AddRow(
                $"[bold {svc.Color}]{svc.Name}[/]",
                svc.Port.HasValue ? $"[bold cyan underline]{urlStr}[/]" : "[grey]Internal / Daemon[/]",
                $"[white]{Path.GetFileName(svc.WorkingDirectory)}[/]",
                $"[grey]{Markup.Escape(svc.RawCommand)}[/]"
            );
        }

        AnsiConsole.MarkupLine("[bold green]🚀 Services Ready & Starting Parallel Execution:[/]");
        AnsiConsole.Write(urlTable);

        // Print tip for swagger/api docs if an api service exists
        var apiSvc = manager.Services.FirstOrDefault(s => s.Name.Contains("api", StringComparison.OrdinalIgnoreCase) && s.Port.HasValue);
        if (apiSvc != null && apiSvc.Port.HasValue)
        {
            AnsiConsole.MarkupLine($"[grey]📖 API Swagger Docs:[/] [cyan underline]http://localhost:{apiSvc.Port.Value}/api/docs[/] [grey]or[/] [cyan underline]http://localhost:{apiSvc.Port.Value}/swagger[/]");
        }
        AnsiConsole.WriteLine();

        if (openInBrowser)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(2500);
                foreach (var svc in manager.Services.Where(s => s.Port.HasValue))
                {
                    TryOpenBrowser($"http://localhost:{svc.Port!.Value}");
                }
            });
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            AnsiConsole.MarkupLine("\n[bold red]Stopping all services gracefully...[/]");
            cts.Cancel();
        };

        AnsiConsole.MarkupLine("[bold]Streaming logs... (Press [red]Ctrl+C[/] to stop)[/]\n");

        await manager.RunAllAsync(
            logCallback: (serviceName, line, isError) =>
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
            },
            diagnosisCallback: diag =>
            {
                var panel = new Panel(new Markup(
                    $"[bold red]Issue:[/] {Markup.Escape(diag.Summary)}\n\n" +
                    $"[bold yellow]💡 Why it happened:[/] {Markup.Escape(diag.WhyItHappened)}\n\n" +
                    $"[bold green]👉 Suggested Fix:[/] {Markup.Escape(diag.SuggestedRemediation)}"
                ))
                {
                    Header = new PanelHeader($"[bold red] Diagnostic Failure in '{diag.ServiceName}' (Exit code: {diag.ExitCode}) [/]"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Red)
                };

                AnsiConsole.WriteLine();
                AnsiConsole.Write(panel);
                AnsiConsole.WriteLine();
            },
            cancellationToken: cts.Token
        );

        return 0;
    }

    private static int RunOpen()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(currentDir, "omni.yaml");
        var config = OmniConfig.LoadFromFile(configPath);

        if (config == null || !config.Services.Any())
        {
            AnsiConsole.MarkupLine("[red]No omni.yaml found or no services configured.[/]");
            return 1;
        }

        int opened = 0;
        foreach (var (name, svc) in config.Services)
        {
            if (svc.Port.HasValue)
            {
                var url = $"http://localhost:{svc.Port.Value}";
                AnsiConsole.MarkupLine($"[green]Opening[/] [cyan]{name}[/] at [cyan underline]{url}[/]");
                TryOpenBrowser(url);
                opened++;
            }
        }

        if (opened == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No services have a declared port to open.[/]");
        }
        return 0;
    }

    private static void TryOpenBrowser(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start {url}",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start("xdg-open", url);
            }
        }
        catch { }
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

    private static int ShowHelp(string? unknownCommand = null)
    {
        if (!string.IsNullOrEmpty(unknownCommand))
        {
            AnsiConsole.MarkupLine($"[red]Unknown command:[/] '[yellow]{Markup.Escape(unknownCommand)}[/]'\n");
        }

        AnsiConsole.MarkupLine("[bold]Usage:[/] [cyan]omni[/] [yellow]<command>[/] [grey][[options]][/]\n");

        var table = new Table().Border(TableBorder.None).HideHeaders();
        table.AddColumn("Command");
        table.AddColumn("Description");

        table.AddRow("[bold cyan]dev [[--open|-o]] [[--kill|-k]][/]", "Run all services concurrently. Auto-remaps occupied ports to free ones (or --kill to force stop).");
        table.AddRow("[bold cyan]open[/]", "Launch configured service URLs directly in your default web browser.");
        table.AddRow("[bold cyan]doctor[/]", "Run a pre-flight health check on host tools (.NET, Node, Python, Docker) & check port availability.");
        table.AddRow("[bold cyan]analyze[/]", "Scan workspace, detect all technology stacks, and print component layout.");
        table.AddRow("[bold cyan]init[/]", "Create an annotated [yellow]omni.yaml[/] template in the current directory.");
        table.AddRow("[bold cyan]run <task>[/]", "Execute defined pipeline tasks (e.g. [yellow]build[/], [yellow]test[/]) sequentially or in parallel.");
        table.AddRow("[bold cyan]--version, -v[/]", "Display version information.");
        table.AddRow("[bold cyan]--help, -h[/]", "Display this help message.");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[bold]Quick Examples:[/] ");
        AnsiConsole.MarkupLine("  [grey]$[/] [cyan]omni dev[/]                 [grey]# Start entire stack with automatic port conflict remapping[/]");
        AnsiConsole.MarkupLine("  [grey]$[/] [cyan]omni dev --open[/]          [grey]# Start entire stack and open in browser automatically[/]");
        AnsiConsole.MarkupLine("  [grey]$[/] [cyan]omni open[/]                [grey]# Open active frontend/API URLs in default browser[/]");
        AnsiConsole.MarkupLine("  [grey]$[/] [cyan]omni doctor[/]              [grey]# Check runtimes and verify port availability[/]");
        AnsiConsole.MarkupLine("  [grey]$[/] [cyan]omni run test[/]           [grey]# Execute all project test suites[/]");
        AnsiConsole.MarkupLine("  [grey]$[/] [cyan]omni init[/]               [grey]# Generate custom omni.yaml[/]");

        return 0;
    }
}
