#:project ../.github/BuildHelpers/BuildHelpers.csproj

using static BuildHelpers.Targets;

SharedTargets(
    "hybrid-cache-handler/hybrid-cache-handler.slnf",
    "hybrid-cache-handler/src/HttpHybridCacheHandler",
    registerPack: false);

var packages = new (string Key, string Project)[]
{
    ("handler", "HttpHybridCacheHandler"),
    ("contentstore", "HttpHybridCacheHandler.ContentStore"),
    ("azureblob", "HttpHybridCacheHandler.ContentStore.AzureBlob"),
    ("s3", "HttpHybridCacheHandler.ContentStore.S3"),
    ("gcs", "HttpHybridCacheHandler.ContentStore.GoogleCloudStorage"),
    ("filesystem", "HttpHybridCacheHandler.ContentStore.FileSystem"),
};

foreach (var (key, project) in packages)
{
    PackTarget($"pack-{key}", $"hybrid-cache-handler/src/{project}",
        $"hybrid-cache-handler/artifacts/{key}");
    if (key != "contentstore")
    {
        TestTarget($"test-{key}", $"hybrid-cache-handler/test/{project}.Tests");
    }
}

// Preserve the product's single-package default; use pack-all explicitly for local/CI bundles.
AggregateTarget(Pack, ["pack-handler"]);
AggregateTarget("pack-all", packages.Select(package => $"pack-{package.Key}"));
AggregateTarget(Test, packages.Where(package => package.Key != "contentstore")
    .Select(package => $"test-{package.Key}"));

DefaultTarget(dependsOn:
[
    Build,
    Test,
]);

await RunTargetsAndExitAsync(args);
