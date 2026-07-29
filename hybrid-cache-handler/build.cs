#:project ../.github/BuildHelpers/BuildHelpers.csproj

using static BuildHelpers.Targets;

SharedTargets(
    "hybrid-cache-handler/hybrid-cache-handler.slnf",
    "hybrid-cache-handler/src/HttpHybridCacheHandler");

TestTarget(Test, "hybrid-cache-handler/test/HttpHybridCacheHandler.Tests");

DefaultTarget(dependsOn:
[
    Build,
    Test,
]);

await RunTargetsAndExitAsync(args);
