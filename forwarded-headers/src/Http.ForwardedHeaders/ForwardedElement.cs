// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.ObjectModel;

namespace DamianH.Http.ForwardedHeaders;

/// <summary>
/// An immutable Forwarded element, retaining standard and extension parameters without semantic validation.
/// </summary>
public sealed class ForwardedElement
{
    internal ForwardedElement(Dictionary<string, string> parameters, int prefixLength)
    {
        Parameters = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(parameters, StringComparer.OrdinalIgnoreCase));
        PrefixLength = prefixLength;
    }

    /// <summary>
    /// Gets a read-only snapshot of the parameters, matched case-insensitively.
    /// Quoted values are unquoted and quoted-pair escapes are decoded.
    /// </summary>
    public IReadOnlyDictionary<string, string> Parameters { get; }

    /// <summary>Gets the unvalidated <c>for</c> value, or <see langword="null"/> if absent.</summary>
    public string? For => GetParameter("for");

    /// <summary>Gets the unvalidated <c>by</c> value, or <see langword="null"/> if absent.</summary>
    public string? By => GetParameter("by");

    /// <summary>Gets the unvalidated <c>host</c> value, or <see langword="null"/> if absent.</summary>
    public string? Host => GetParameter("host");

    /// <summary>Gets the unvalidated <c>proto</c> value, or <see langword="null"/> if absent.</summary>
    public string? Proto => GetParameter("proto");

    internal int PrefixLength { get; }

    private string? GetParameter(string name) => Parameters.TryGetValue(name, out var value) ? value : null;
}
