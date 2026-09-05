// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;

namespace DamianH.Http.StructuredFieldValues.RfcCompliance;

/// <summary>
/// Loads every checked-in RFC fixture copied to the test output directory.
/// </summary>
public static class RfcTestLoader
{
    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "RfcTests");

    public static IReadOnlyDictionary<string, RfcTestCase[]> Fixtures { get; } = LoadFixtures();

    private static IReadOnlyDictionary<string, RfcTestCase[]> LoadFixtures()
    {
        var fixtures = new Dictionary<string, RfcTestCase[]>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(FixtureDirectory, "*.json", SearchOption.AllDirectories)
                     .Order(StringComparer.Ordinal))
        {
            var fileName = Path.GetRelativePath(FixtureDirectory, path);
            var tests = JsonSerializer.Deserialize<RfcTestCase[]>(File.ReadAllText(path))
                ?? throw new InvalidDataException($"Fixture '{fileName}' contains no test array.");
            if (tests.Length == 0)
            {
                throw new InvalidDataException($"Fixture '{fileName}' contains no test cases.");
            }

            fixtures.Add(fileName, tests);
        }

        if (fixtures.Count == 0)
        {
            throw new InvalidDataException($"No RFC fixtures found in '{FixtureDirectory}'.");
        }

        return fixtures;
    }
}
