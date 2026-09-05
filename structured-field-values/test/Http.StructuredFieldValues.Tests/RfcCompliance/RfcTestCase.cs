// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DamianH.Http.StructuredFieldValues.RfcCompliance;

/// <summary>
/// Represents a test case from the official Structured Field Values test suite.
/// </summary>
public class RfcTestCase
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("raw")]
    public string[] Raw { get; set; } = [];

    [JsonPropertyName("header_type")]
    public string? HeaderType { get; set; }

    [JsonPropertyName("expected")]
    public JsonElement? Expected { get; set; }

    [JsonPropertyName("must_fail")]
    public bool MustFail { get; set; }

    [JsonPropertyName("can_fail")]
    public bool CanFail { get; set; }

    [JsonPropertyName("canonical")]
    public string[]? Canonical { get; set; }

    public override string ToString() => Name;
}
