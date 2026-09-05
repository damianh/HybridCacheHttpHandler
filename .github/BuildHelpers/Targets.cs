using static Bullseye.Targets;
using static SimpleExec.Command;

namespace BuildHelpers;

/// <summary>
/// Registers build targets for per-lib build scripts.
/// </summary>
public static class Targets
{
    private static readonly Lazy<string> RepoRoot = new(FindRoot);

    public const string Restore = "restore";
    public const string Build = "build";
    public const string Clean = "clean";
    public const string Pack = "pack";
    public const string Test = "test";

    private const string Default = "default";

    /// <summary>
    /// Registers all shared targets parameterized by the lib's solution filter path.
    /// </summary>
    /// <param name="slnfPath">
    /// Repo-relative path to the lib's solution filter
    /// (e.g. <c>signatures/signatures.slnf</c>).
    /// </param>
    /// <param name="packProjectPath">
    /// Repo-relative path to the lib's packable src project
    /// (e.g. <c>signatures/src/Http.HttpSignatures</c>).
    /// </param>
    /// <param name="registerPack">Whether to register the default single-project pack target.</param>
    public static void SharedTargets(string slnfPath, string packProjectPath, bool registerPack = true)
    {
        ArgumentNullException.ThrowIfNull(slnfPath);
        ArgumentNullException.ThrowIfNull(packProjectPath);

        var libDir = slnfPath.Split('/')[0];

        Target(Restore, () =>
            RunAsync("dotnet", $"restore {slnfPath}", RepoRoot.Value));

        Target(Build, dependsOn: [Restore], () =>
            RunAsync("dotnet", $"build {slnfPath} --no-restore -c Release", RepoRoot.Value));

        Target(Clean, () =>
            RunAsync("dotnet", $"clean {slnfPath}", RepoRoot.Value));

        if (registerPack)
        {
            PackTarget(Pack, packProjectPath, $"{libDir}/artifacts");
        }
    }

    /// <summary>
    /// Registers a pack target for one project and its isolated artifact directory.
    /// </summary>
    public static void PackTarget(string targetName, string packProjectPath, string artifactsPath) =>
        Target(targetName, dependsOn: [Build], () =>
            RunAsync(
                "dotnet",
                $"pack {packProjectPath} --no-build -c Release -o {artifactsPath}",
                RepoRoot.Value));

    public static void AggregateTarget(string targetName, IEnumerable<string> dependsOn) =>
        Target(targetName, dependsOn);

    /// <summary>
    /// Registers a test target that runs <c>dotnet test</c> on a test project with standard options.
    /// </summary>
    /// <param name="targetName">The target name (e.g. <c>"test"</c>).</param>
    /// <param name="testProjectPath">
    /// Repo-relative path to the test project
    /// (e.g. <c>"signatures/test/Http.HttpSignatures.Tests"</c>).
    /// </param>
    public static void TestTarget(string targetName, string testProjectPath) =>
        Target(targetName, dependsOn: [Restore], () =>
            RunAsync(
                "dotnet",
                $"test --project {testProjectPath} -c Release --no-restore --report-xunit-trx " +
                    $"--report-xunit-trx-filename {testProjectPath.Replace('/', '-')}-tests.trx",
                RepoRoot.Value));

    public static void DefaultTarget(IEnumerable<string> dependsOn) =>
        Target(Default, dependsOn);

    public static Task RunTargetsAndExitAsync(IEnumerable<string> args) =>
        Bullseye.Targets.RunTargetsAndExitAsync(args, messageOnly: ex => ex is SimpleExec.ExitCodeException);

    private static string FindRoot()
    {
        var root = Directory.GetCurrentDirectory();

        // Repositories have a .git folder, worktrees have a .git file, so check for both.
        while (!Directory.Exists(Path.Combine(root, ".git")) && !File.Exists(Path.Combine(root, ".git")))
        {
            root = Directory.GetParent(root) is { } parent
                ? parent.FullName
                : throw new InvalidOperationException(
                    "Could not find repository root (no .git directory or file found)");
        }

        return root;
    }
}
