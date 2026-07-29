#:project ../.github/BuildHelpers/BuildHelpers.csproj

using static BuildHelpers.Targets;

SharedTargets(
    "structured-field-values/structured-field-values.slnf",
    "structured-field-values/src/Http.StructuredFieldValues");

TestTarget(Test, "structured-field-values/test/Http.StructuredFieldValues.Tests");

DefaultTarget(dependsOn:
[
    Build,
    Test,
]);

await RunTargetsAndExitAsync(args);
