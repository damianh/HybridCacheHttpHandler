#:project ../.github/BuildHelpers/BuildHelpers.csproj

using static BuildHelpers.Targets;

SharedTargets(
    "forwarded-headers/forwarded-headers.slnf",
    "forwarded-headers/src/Http.ForwardedHeaders");

TestTarget(Test, "forwarded-headers/test/Http.ForwardedHeaders.Tests");

DefaultTarget(dependsOn: [Build, Test]);

await RunTargetsAndExitAsync(args);
