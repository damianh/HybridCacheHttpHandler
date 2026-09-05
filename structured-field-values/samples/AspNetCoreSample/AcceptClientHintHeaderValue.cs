// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.StructuredFieldValues;

namespace AspNetCoreSample;

/// <summary>
/// Represents an Accept-CH (Accept Client Hints) header value as defined in RFC 8942.
/// The header contains a list of client hint tokens that the server wishes to receive.
/// </summary>
public class AcceptClientHintHeaderValue
{
    /// <summary>Gets the requested client hint header names as tokens.</summary>
    public IReadOnlyList<string> Hints { get; init; } = [];

    /// <summary>Checks if a specific client hint is requested.</summary>
    /// <param name="hintName">The client hint header name (e.g., "Sec-CH-UA").</param>
    /// <returns>True if the hint is requested, false otherwise.</returns>
    public bool Contains(string hintName)
    {
        ArgumentNullException.ThrowIfNull(hintName);
        return Hints.Any(t => string.Equals(t, hintName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Mapper for parsing and serializing Accept-CH headers.</summary>
    public static readonly StructuredFieldMapper<AcceptClientHintHeaderValue> Mapper =
        StructuredFieldMapper<AcceptClientHintHeaderValue>.List(b => b
            .Elements(x => x.Hints, type: ItemType.Token));
}
