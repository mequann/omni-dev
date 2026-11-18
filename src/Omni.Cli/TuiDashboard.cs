using Spectre.Console;
using Omni.Core.DevRunner;
using Omni.Core.Diagnostics;

namespace Omni.Cli;

public class TuiDashboard
{
    public static async Task RunAsync(
        ConcurrentManager manager,
        Task runAllTask,
        CancellationTokenSource cts,
        Action<string>? onOpenBrowser = null)
    {
        var services = manager.Services;
        if (!services.Any())
            return;

        var layout = BuildLayout(services);

        // Background keyboard handler
        _ = Task.Run(async () =>
        {
            while (!cts.Token.IsCancellationRequested && !runAllTask.IsCompleted)
            {
                try
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true);
                        if (key.Key == ConsoleKey.Q)
                        {
                            cts.Cancel();
                            break;
                        }
                        else if (key.Key == ConsoleKey.O)
                        {
                            foreach (var s in services.Where(s => s.Port.HasValue))
                            {
                                onOpenBrowser?.Invoke($"http://localhost:{s.Port!.Value}");
                            }
                        }
                        else if (key.Key == ConsoleKey.C)
                        {
                            foreach (var s in services)
                            {
                                manager.ClearServiceBuffer(s.Name);
                            }
                        }
                    }
                }
                catch { }

                try
                {
                    await Task.Delay(60, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        });

        // Live display loop
        try
        {
            await AnsiConsole.Live(layout)
                .AutoClear(false)
                .Overflow(VerticalOverflow.Crop)
                .StartAsync(async ctx =>
                {
                    while (!runAllTask.IsCompleted && !cts.Token.IsCancellationRequested)
                    {
                        RefreshPanels(layout, manager);
                        ctx.Refresh();

                        try
                        {
                            await Task.Delay(120, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }

                    RefreshPanels(layout, manager);
                    ctx.Refresh();
                });
        }
        catch (OperationCanceledException) { }
    }

    private static Layout BuildLayout(IReadOnlyList<ServiceSpec> services)
    {
        var root = new Layout("Root");
        root.SplitRows(
            new Layout("Services"),
            new Layout("Footer").Size(3)
        );

        var servicesLayout = root["Services"];

        if (services.Count == 1)
        {
            servicesLayout.Update(new Panel(new Markup("[grey]Starting...[/]")));
        }
        else if (services.Count == 2)
        {
            servicesLayout.SplitColumns(
                new Layout(services[0].Name),
                new Layout(services[1].Name)
            );
        }
        else if (services.Count == 3)
        {
            servicesLayout.SplitColumns(
                new Layout(services[0].Name),
                new Layout("Right").SplitRows(
                    new Layout(services[1].Name),
                    new Layout(services[2].Name)
                )
            );
        }
        else
        {
            // 4+ services: 2x2 grid
            var topCols = new List<Layout>();
            var botCols = new List<Layout>();

            int half = (services.Count + 1) / 2;
            for (int i = 0; i < services.Count; i++)
            {
                if (i < half)
                    topCols.Add(new Layout(services[i].Name));
                else
                    botCols.Add(new Layout(services[i].Name));
            }

            var topLayout = new Layout("Top");
            topLayout.SplitColumns(topCols.ToArray());

            var botLayout = new Layout("Bottom");
            botLayout.SplitColumns(botCols.ToArray());

            servicesLayout.SplitRows(topLayout, botLayout);
        }

        root["Footer"].Update(CreateFooterPanel());
        return root;
    }

    private static void RefreshPanels(Layout root, ConcurrentManager manager)
    {
        var services = manager.Services;

        // Terminal height budgeting
        int termHeight = 24;
        try { termHeight = Math.Max(Console.WindowHeight, 18); } catch { }
        int visibleLines = services.Count <= 2 ? Math.Max(termHeight - 8, 8) : Math.Max((termHeight - 10) / 2, 5);

        foreach (var svc in services)
        {
            try
            {
                var targetLayout = services.Count == 1 ? root["Services"] : root[svc.Name];
                if (targetLayout != null)
                {
                    targetLayout.Update(CreateServicePanel(svc, manager, visibleLines));
                }
            }
            catch { }
        }

        root["Footer"].Update(CreateFooterPanel());
    }

    private static Panel CreateServicePanel(ServiceSpec svc, ConcurrentManager manager, int maxLines)
    {
        var state = manager.GetServiceState(svc.Name);
        var exitCode = manager.GetExitCode(svc.Name);
        var buffer = manager.GetServiceBuffer(svc.Name);

        // Header info
        string portBadge = svc.Port.HasValue ? $" [cyan]({svc.Port.Value})[/]" : "";
        string statusBadge = state switch
        {
            ServiceState.Starting => "[yellow]⟳ Starting[/]",
            ServiceState.Running => "[bold green]● Live[/]",
            ServiceState.ExitedOk => "[grey]✔ Stopped[/]",
            ServiceState.Failed => $"[bold red]✘ Failed ({exitCode})[/]",
            _ => ""
        };

        var title = $"[bold {svc.Color}]{svc.Name}[/]{portBadge} {statusBadge}";

        // Content lines
        var recentLines = buffer.TakeLast(maxLines).ToList();
        string contentMarkup;

        if (!recentLines.Any())
        {
            contentMarkup = "[grey italic]Waiting for logs...[/]";
        }
        else
        {
            contentMarkup = string.Join("\n", recentLines.Select(l =>
            {
                var escaped = Markup.Escape(l);
                if (escaped.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                    escaped.Contains("fail", StringComparison.OrdinalIgnoreCase))
                {
                    return $"[red]{escaped}[/]";
                }
                if (escaped.Contains("warn", StringComparison.OrdinalIgnoreCase))
                {
                    return $"[yellow]{escaped}[/]";
                }
                return $"[white]{escaped}[/]";
            }));
        }

        var borderColor = state switch
        {
            ServiceState.Failed => Color.Red,
            ServiceState.Running => Color.Green,
            _ => Color.Cyan1
        };

        return new Panel(new Markup(contentMarkup))
        {
            Header = new PanelHeader(title),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor),
            Expand = true
        };
    }

    private static Panel CreateFooterPanel()
    {
        return new Panel(
            new Markup("[bold cyan]Omni Multi-Pane TUI[/] | [white]Shortcuts:[/] [cyan][o][/] Open Browser | [yellow][c][/] Clear | [red][q / Ctrl+C][/] Stop")
        )
        {
            Border = BoxBorder.Square,
            BorderStyle = new Style(Color.Grey),
            Expand = true
        };
    }
}
