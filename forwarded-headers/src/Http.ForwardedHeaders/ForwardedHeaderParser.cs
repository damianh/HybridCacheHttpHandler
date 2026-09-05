// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace DamianH.Http.ForwardedHeaders;

/// <summary>
/// Parses RFC 7239 Forwarded field syntax independently of node, host, and scheme semantics.
/// </summary>
public static class ForwardedHeaderParser
{
    /// <summary>Parses one Forwarded field value.</summary>
    /// <param name="value">The original field value.</param>
    /// <returns>The immutable parsed header.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="FormatException">The field syntax is invalid.</exception>
    public static ForwardedHeader Parse(string value)
    {
        if (!TryParse(value, out var header, out var error))
        {
            throw new FormatException($"{error.Message} (position {error.Position}).");
        }

        return header;
    }

    /// <summary>Parses ordered Forwarded field values as a single comma-joined field.</summary>
    /// <param name="values">The ordered field values. Null members are treated as empty field values.</param>
    /// <returns>The immutable parsed header.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    /// <exception cref="FormatException">The combined field syntax is invalid.</exception>
    public static ForwardedHeader Parse(IEnumerable<string?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return Parse(string.Join(",", values));
    }

    /// <summary>Attempts to parse one Forwarded field value without throwing for malformed syntax.</summary>
    /// <param name="value">The original field value.</param>
    /// <param name="header">The parsed header on success; otherwise null.</param>
    /// <param name="error">The safe diagnostic on failure; otherwise null.</param>
    /// <returns>Whether the field syntax is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public static bool TryParse(
        string value,
        [NotNullWhen(true)] out ForwardedHeader? header,
        [NotNullWhen(false)] out ForwardedParseError? error)
    {
        ArgumentNullException.ThrowIfNull(value);
        var parser = new ForwardedFieldParser(value);
        return parser.TryParse(out header, out error);
    }

    /// <summary>Attempts to parse ordered field values as a single comma-joined field.</summary>
    /// <param name="values">The ordered field values. Null members are treated as empty field values.</param>
    /// <param name="header">The parsed header on success; otherwise null.</param>
    /// <param name="error">The safe diagnostic on failure; otherwise null.</param>
    /// <returns>Whether the combined field syntax is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    public static bool TryParse(
        IEnumerable<string?> values,
        [NotNullWhen(true)] out ForwardedHeader? header,
        [NotNullWhen(false)] out ForwardedParseError? error)
    {
        ArgumentNullException.ThrowIfNull(values);
        return TryParse(string.Join(",", values), out header, out error);
    }
}
