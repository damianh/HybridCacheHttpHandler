// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Synthetic structured dictionary for testing nullable integers and Boolean members.
/// This is not a model of a standardized HTTP header.
/// </summary>
public class SyntheticDictionaryModel
{
    public int? Limit { get; init; }
    public int? Offset { get; init; }
    public bool? Enabled { get; init; }
    public bool? Audited { get; init; }
    public bool? Durable { get; init; }
    public bool? Local { get; init; }
    public bool? Shared { get; init; }
}
