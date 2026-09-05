// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Convenience combined-value accessors for <see cref="IHttpMessageContext"/>, implemented as
/// extension methods over the header/trailer raw-value members so every adapter gets identical,
/// centralized canonicalization (RFC 9421 §2.1) without also having to implement its own
/// independently canonicalized combined-value API.
/// </summary>
public static class HttpMessageContextExtensions
{
    /// <summary>
    /// Gets the combined, canonicalized value for the named header field, or null when the header
    /// is not present. Multiple field lines are combined per RFC 9110 §5.2 (comma + space).
    /// </summary>
    /// <param name="context">The message context.</param>
    /// <param name="fieldName">The header field name.</param>
    /// <exception cref="FormatException">
    /// Thrown when a raw value contains an illegal line break or control character that cannot be
    /// resolved by unfolding obsolete line folding.
    /// </exception>
    public static string? GetHeaderValue(this IHttpMessageContext context, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(context);
        return HttpFieldValueCanonicalizer.Combine(fieldName, "header", context.GetHeaderValues(fieldName));
    }

    /// <summary>
    /// Gets the combined, canonicalized value for the named trailer field, or null when the trailer
    /// is not present. Multiple field lines are combined per RFC 9110 §5.2 (comma + space). Never
    /// falls back to a header of the same name.
    /// </summary>
    /// <param name="context">The message context.</param>
    /// <param name="fieldName">The trailer field name.</param>
    /// <exception cref="FormatException">
    /// Thrown when a raw value contains an illegal line break or control character that cannot be
    /// resolved by unfolding obsolete line folding.
    /// </exception>
    public static string? GetTrailerValue(this IHttpMessageContext context, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(context);
        return HttpFieldValueCanonicalizer.Combine(fieldName, "trailer", context.GetTrailerValues(fieldName));
    }
}

/// <summary>
/// Centralizes whitespace trimming, obsolete line-fold unwrapping, and comma-space combination
/// for raw HTTP field values (RFC 9110 §5.2, §5.5). Rejects any line break or control character
/// that remains after unfolding, rather than silently passing it through.
/// </summary>
internal static class HttpFieldValueCanonicalizer
{
    internal static string? Combine(string fieldName, string section, IReadOnlyList<string> rawValues)
    {
        ArgumentNullException.ThrowIfNull(rawValues);

        if (rawValues.Count == 0)
            return null;

        if (rawValues.Count == 1)
            return CanonicalizeSingle(fieldName, section, rawValues[0]);

        var sb = new StringBuilder();
        for (var i = 0; i < rawValues.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(CanonicalizeSingle(fieldName, section, rawValues[i]));
        }

        return sb.ToString();
    }

    internal static string CanonicalizeSingle(string fieldName, string section, string rawValue)
    {
        var unfolded = UnfoldObsFold(rawValue);

        // RFC 9110 §5.6.3: optional whitespace (OWS) is SP / HTAB, trimmed at each end.
        var trimmed = unfolded.Trim(' ', '\t');

        ValidateNoIllegalCharacters(fieldName, section, trimmed);
        return trimmed;
    }

    /// <summary>
    /// Collapses supported obsolete line folding (historically CRLF or a bare LF followed by one
    /// or more SP/HTAB) to a single space, per RFC 9112 §5.1's obs-fold compatibility note. Any
    /// other embedded line break is left untouched so it is rejected below rather than silently
    /// accepted.
    /// </summary>
    private static string UnfoldObsFold(string value)
    {
        if (!value.Contains('\r') && !value.Contains('\n'))
            return value;

        var sb = new StringBuilder(value.Length);
        var i = 0;
        while (i < value.Length)
        {
            var c = value[i];
            var foldLength = 0;

            if (c == '\r' && i + 1 < value.Length && value[i + 1] == '\n')
                foldLength = 2;
            else if (c == '\n')
                foldLength = 1;

            if (foldLength > 0 && i + foldLength < value.Length && IsFoldWhitespace(value[i + foldLength]))
            {
                sb.Append(' ');
                i += foldLength;
                while (i < value.Length && IsFoldWhitespace(value[i]))
                    i++;
                continue;
            }

            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    private static bool IsFoldWhitespace(char c) => c is ' ' or '\t';

    private static void ValidateNoIllegalCharacters(string fieldName, string section, string value)
    {
        foreach (var c in value)
        {
            if (c is '\r' or '\n')
            {
                throw new FormatException(
                    $"The {section} field '{fieldName}' contains a line break that could not be resolved " +
                    "as obsolete line folding.");
            }

            // RFC 9110 §5.5: field-content is VCHAR / obs-text, optionally interspersed with SP/HTAB.
            // Reject remaining C0 controls (other than HTAB, already handled above) and DEL.
            if ((c < 0x20 && c != '\t') || c == 0x7F)
            {
                throw new FormatException(
                    $"The {section} field '{fieldName}' contains an illegal control character (0x{(int)c:X2}).");
            }
        }
    }
}
