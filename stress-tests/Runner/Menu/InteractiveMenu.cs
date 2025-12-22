using Runner.Config;
using Runner.Infrastructure;
using Runner.Suites;
using Spectre.Console;

namespace Runner.Menu;

public class InteractiveMenu
{
    private readonly IEnumerable<ISuite> _suites;
    private readonly CachedClientFactory _clientFactory;
    private readonly ResultsPresenter _presenter;
    private SuiteConfig _config;

    public InteractiveMenu(
        IEnumerable<ISuite> suites,
        CachedClientFactory clientFactory,
        ResultsPresenter presenter)
    {
        _suites = suites;
        _clientFactory = clientFactory;
        _presenter = presenter;
        _config = new SuiteConfig(); // Default config
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        ShowWelcome();

        while (!cancellationToken.IsCancellationRequested)
        {
            var choice = ShowMainMenu();

            switch (choice)
            {
                case "Run Suite":
                    await RunSuiteAsync(cancellationToken);
                    break;
                case "Run All Suites":
                    await RunAllSuitesAsync(cancellationToken);
                    break;
                case "Configure":
                    ConfigureSettings();
                    break;
                case "View Configuration":
                    ViewConfiguration();
                    break;
                case "Exit":
                    return;
            }
        }
    }

    private void ShowWelcome()
    {
        AnsiConsole.Clear();
        
        var rule = new Rule("[yellow bold]HybridCacheHttpHandler Stress Test Runner[/]")
        {
            Justification = Justify.Center
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
    }

    private string ShowMainMenu()
    {
        AnsiConsole.Clear();
        ShowWelcome();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]What would you like to do?[/]")
                .PageSize(10)
                .AddChoices(new[]
                {
                    "Run Suite",
                    "Run All Suites",
                    "Configure",
                    "View Configuration",
                    "Exit"
                }));

        return choice;
    }

    private async Task RunSuiteAsync(CancellationToken cancellationToken)
    {
        var suiteList = _suites.ToList();
        
        if (!suiteList.Any())
        {
            AnsiConsole.MarkupLine("[red]No test suites available![/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey();
            return;
        }

        var selectedSuiteName = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select a test suite to run:[/]")
                .PageSize(10)
                .AddChoices(suiteList.Select(s => s.Name)));

        var suite = suiteList.First(s => s.Name == selectedSuiteName);

        AnsiConsole.Clear();
        ShowWelcome();
        
        var panel = new Panel(
            new Markup($"[bold]{suite.Name}[/]\n\n{Markup.Escape(suite.Description)}\n\n[dim]Estimated duration: {suite.EstimatedDuration.TotalSeconds:F0}s[/]"))
        {
            Header = new PanelHeader("[yellow]Test Suite Details[/]"),
            Border = BoxBorder.Rounded
        };
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("Run this suite?"))
        {
            return;
        }

        await ExecuteSuiteAsync(suite, cancellationToken);
    }

    private async Task RunAllSuitesAsync(CancellationToken cancellationToken)
    {
        var suiteList = _suites.ToList();
        
        if (!suiteList.Any())
        {
            AnsiConsole.MarkupLine("[red]No test suites available![/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
            Console.ReadKey();
            return;
        }

        AnsiConsole.Clear();
        ShowWelcome();
        
        AnsiConsole.MarkupLine($"[yellow]Running all {suiteList.Count} test suites...[/]");
        AnsiConsole.WriteLine();

        if (!AnsiConsole.Confirm("Continue?"))
        {
            return;
        }

        var allResults = new List<(string SuiteName, SuiteResult Result)>();

        for (var i = 0; i < suiteList.Count; i++)
        {
            var suite = suiteList[i];
            
            AnsiConsole.Clear();
            ShowWelcome();
            AnsiConsole.MarkupLine($"[yellow]Running suite {i + 1} of {suiteList.Count}[/]");
            AnsiConsole.WriteLine();

            var result = await ExecuteSuiteAsync(suite, cancellationToken, showResultsAfter: false);
            allResults.Add((suite.Name, result));
            
            if (i < suiteList.Count - 1)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Press any key to continue to next suite...[/]");
                Console.ReadKey();
            }
        }

        ShowAllSuitesResults(allResults);
    }

    private async Task<SuiteResult> ExecuteSuiteAsync(ISuite suite, CancellationToken cancellationToken, bool showResultsAfter = true)
    {
        var client = _clientFactory.CreateClient();
        
        var result = await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[green]Running {suite.Name}[/]", maxValue: _config.TotalRequests);

                var progress = new Progress<SuiteProgress>(p =>
                {
                    task.Value = p.CompletedRequests;
                    task.Description = $"[green]{Markup.Escape(p.CurrentPhase)}[/] " +
                                      $"(Hits: {p.CacheHits}, Misses: {p.CacheMisses}, Avg: {p.AverageLatencyMs:F2}ms)";
                });

                return await suite.RunAsync(client, _config, progress, cancellationToken);
            });

        if (showResultsAfter)
        {
            _presenter.ShowResults(result, suite.Name);
        }

        return result;
    }

    private void ShowAllSuitesResults(List<(string SuiteName, SuiteResult Result)> results)
    {
        AnsiConsole.Clear();
        ShowWelcome();
        
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold]Suite[/]");
        table.AddColumn("[bold]Status[/]");
        table.AddColumn("[bold]Duration[/]");
        table.AddColumn("[bold]Requests[/]");
        table.AddColumn("[bold]Cache Hit %[/]");
        table.AddColumn("[bold]P95 Latency[/]");

        foreach (var (suiteName, result) in results)
        {
            var status = result.Success ? "[green]✓ PASSED[/]" : "[red]✗ FAILED[/]";
            var duration = $"{result.Duration.TotalSeconds:F2}s";
            var requests = result.Metrics.TotalRequests.ToString();
            var hitRatio = $"{result.Metrics.CacheHitRatio:P1}";
            var p95 = $"{result.Metrics.Latency.P95Ms:F2}ms";

            table.AddRow(Markup.Escape(suiteName), status, duration, requests, hitRatio, p95);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        var totalPassed = results.Count(r => r.Result.Success);
        var totalFailed = results.Count - totalPassed;

        AnsiConsole.MarkupLine($"[bold]Summary:[/] {totalPassed} passed, {totalFailed} failed");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        Console.ReadKey();
    }

    private void ConfigureSettings()
    {
        AnsiConsole.Clear();
        ShowWelcome();

        var configChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]What would you like to configure?[/]")
                .AddChoices(new[]
                {
                    "Concurrent Clients",
                    "Total Requests",
                    "Duration",
                    "Target URL",
                    "Enable Diagnostics",
                    "Reset to Defaults",
                    "Back"
                }));

        switch (configChoice)
        {
            case "Concurrent Clients":
                _config = _config with
                {
                    ConcurrentClients = AnsiConsole.Prompt(
                        new TextPrompt<int>("Enter number of concurrent clients:")
                            .DefaultValue(_config.ConcurrentClients)
                            .Validate(n => n > 0 && n <= 1000 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be between 1 and 1000")))
                };
                AnsiConsole.MarkupLine("[green]✓ Updated[/]");
                break;

            case "Total Requests":
                _config = _config with
                {
                    TotalRequests = AnsiConsole.Prompt(
                        new TextPrompt<int>("Enter total number of requests:")
                            .DefaultValue(_config.TotalRequests)
                            .Validate(n => n > 0 && n <= 100000 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be between 1 and 100000")))
                };
                AnsiConsole.MarkupLine("[green]✓ Updated[/]");
                break;

            case "Duration":
                var minutes = AnsiConsole.Prompt(
                    new TextPrompt<int>("Enter duration in minutes:")
                        .DefaultValue((int)_config.Duration.TotalMinutes)
                        .Validate(n => n > 0 && n <= 60 
                            ? ValidationResult.Success() 
                            : ValidationResult.Error("Must be between 1 and 60 minutes")));
                _config = _config with { Duration = TimeSpan.FromMinutes(minutes) };
                AnsiConsole.MarkupLine("[green]✓ Updated[/]");
                break;

            case "Target URL":
                _config = _config with
                {
                    TargetUrl = AnsiConsole.Prompt(
                        new TextPrompt<string>("Enter target URL:")
                            .DefaultValue(_config.TargetUrl))
                };
                AnsiConsole.MarkupLine("[green]✓ Updated[/]");
                break;

            case "Enable Diagnostics":
                _config = _config with
                {
                    IncludeDiagnostics = AnsiConsole.Confirm(
                        "Include diagnostic headers?", 
                        _config.IncludeDiagnostics)
                };
                AnsiConsole.MarkupLine("[green]✓ Updated[/]");
                break;

            case "Reset to Defaults":
                _config = new SuiteConfig();
                AnsiConsole.MarkupLine("[green]✓ Reset to defaults[/]");
                break;

            case "Back":
                return;
        }

        Thread.Sleep(1000);
    }

    private void ViewConfiguration()
    {
        AnsiConsole.Clear();
        ShowWelcome();

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold]Setting[/]");
        table.AddColumn("[bold]Value[/]");

        table.AddRow("Concurrent Clients", _config.ConcurrentClients.ToString());
        table.AddRow("Total Requests", _config.TotalRequests.ToString());
        table.AddRow("Duration", $"{_config.Duration.TotalMinutes:F0} minutes");
        table.AddRow("Target URL", Markup.Escape(_config.TargetUrl));
        table.AddRow("Include Diagnostics", _config.IncludeDiagnostics ? "Yes" : "No");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to continue...[/]");
        Console.ReadKey();
    }
}
