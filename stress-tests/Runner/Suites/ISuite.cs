using Runner.Config;

namespace Runner.Suites;

public interface ISuite
{
    string Name { get; }
    string Description { get; }
    TimeSpan EstimatedDuration { get; }
    
    Task<SuiteResult> RunAsync(
        HttpClient client, 
        SuiteConfig config, 
        IProgress<SuiteProgress> progress,
        CancellationToken cancellationToken);
}
