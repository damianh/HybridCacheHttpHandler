#:project ../.github/BuildHelpers/BuildHelpers.csproj

using static BuildHelpers.Targets;

SharedTargets(
    "signatures/signatures.slnf",
    "signatures/src/Http.HttpSignatures");

TestTarget(Test, "signatures/test/Http.HttpSignatures.Tests");

DefaultTarget(dependsOn:
[
    Build,
    Test,
]);

await RunTargetsAndExitAsync(args);
