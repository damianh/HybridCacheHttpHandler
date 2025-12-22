using Runner.Config;
using Spectre.Console;

namespace Runner.Infrastructure;

public class ResultsPresenter
{
    public void ShowResults(SuiteResult result, string suiteName)
    {
        AnsiConsole.Clear();
        
        var panel = new Panel(new Markup(
            result.Success 
                ? $"[green]{suiteName} - PASSED[/]" 
                : $"[red]{suiteName} - FAILED[/]"))
        {
            Border = BoxBorder.Double
        };
        
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();

        if (!string.IsNullOrEmpty(result.Summary))
        {
            AnsiConsole.MarkupLine($"[bold]Summary:[/]");
            AnsiConsole.WriteLine(result.Summary);
            AnsiConsole.WriteLine();
        }

        if (result.Errors.Count > 0)
        {
            AnsiConsole.MarkupLine($"[red bold]Errors ({result.Errors.Count}):[/]");
            foreach (var error in result.Errors)
            {
                AnsiConsole.MarkupLine($"  [red]✗[/] {Markup.Escape(error)}");
            }
            AnsiConsole.WriteLine();
        }

        ShowMetrics(result.Metrics, result.Duration);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Press any key to return to menu...[/]");
        Console.ReadKey();
    }

    private static void ShowMetrics(SuiteMetrics metrics, TimeSpan duration)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("[bold]Metric[/]");
        table.AddColumn("[bold]Value[/]");

        // Duration
        table.AddRow("Duration", $"{duration.TotalSeconds:F2}s");
        table.AddEmptyRow();

        // Requests
        table.AddRow("Total Requests", metrics.TotalRequests.ToString());
        table.AddRow("Successful", metrics.SuccessfulRequests.ToString());
        table.AddRow("Failed", (metrics.TotalRequests - metrics.SuccessfulRequests).ToString());
        table.AddEmptyRow();

        // Cache
        table.AddRow("Cache Hits", $"{metrics.CacheHits} ({metrics.CacheHitRatio:P1})");
        table.AddRow("Cache Misses", metrics.CacheMisses.ToString());
        table.AddEmptyRow();

        // Latency
        table.AddRow("Latency Min", $"{metrics.Latency.MinMs:F2}ms");
        table.AddRow("Latency Mean", $"{metrics.Latency.MeanMs:F2}ms");
        table.AddRow("Latency Max", $"{metrics.Latency.MaxMs:F2}ms");
        table.AddRow("Latency P95", $"{metrics.Latency.P95Ms:F2}ms");
        table.AddRow("Latency P99", $"{metrics.Latency.P99Ms:F2}ms");
        table.AddEmptyRow();

        // Memory
        table.AddRow("Memory Start", FormatBytes(metrics.Memory.StartBytes));
        table.AddRow("Memory Peak", FormatBytes(metrics.Memory.PeakBytes));
        table.AddRow("Memory End", FormatBytes(metrics.Memory.EndBytes));
        table.AddRow("GC Gen0", metrics.Memory.Gen0Collections.ToString());
        table.AddRow("GC Gen1", metrics.Memory.Gen1Collections.ToString());
        table.AddRow("GC Gen2", metrics.Memory.Gen2Collections.ToString());

        AnsiConsole.Write(table);
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        var order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:F2} {sizes[order]}";
    }
}
