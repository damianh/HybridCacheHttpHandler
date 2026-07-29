#:project ../.github/BuildHelpers/BuildHelpers.csproj

using static BuildHelpers.Targets;

SharedTargets(
    "file-distributed-cache/file-distributed-cache.slnf",
    "file-distributed-cache/src/FileDistributedCache");

TestTarget(Test, "file-distributed-cache/test/FileDistributedCache.Tests");

DefaultTarget(dependsOn:
[
    Build,
    Test,
]);

await RunTargetsAndExitAsync(args);
