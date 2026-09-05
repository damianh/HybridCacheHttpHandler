// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Resolves derived component values from an <see cref="IHttpMessageContext"/>.
/// Derived components start with '@' and represent specific attributes of an HTTP message.
/// RFC 9421 §2.2
/// </summary>
internal static class DerivedComponentResolver
{
    /// <summary>
    /// Resolves the value of a derived component from the given message context.
    /// </summary>
    /// <param name="identifier">The component identifier (must be derived, i.e., start with '@').</param>
    /// <param name="context">The HTTP message context to resolve from.</param>
    /// <returns>The component value string.</returns>
    /// <exception cref="SignatureBaseException">Thrown when the component cannot be resolved.</exception>
    internal static string Resolve(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        ComponentValidator.Validate(identifier, context);

        // If req is set, resolve from the associated request context
        var resolveContext = identifier.Req
            ? context.AssociatedRequest ?? throw new SignatureBaseException(
                identifier,
                "Component has 'req' parameter but no associated request is available.")
            : context;

        return identifier.Name switch
        {
            "@method" => ResolveMethod(identifier, resolveContext),
            "@target-uri" => ResolveTargetUri(identifier, resolveContext),
            "@authority" => ResolveAuthority(identifier, resolveContext),
            "@scheme" => ResolveScheme(identifier, resolveContext),
            "@request-target" => ResolveRequestTarget(identifier, resolveContext),
            "@path" => ResolvePath(identifier, resolveContext),
            "@query" => ResolveQuery(identifier, resolveContext),
            "@query-param" => ResolveQueryParam(identifier, resolveContext),
            "@status" => ResolveStatus(identifier, resolveContext),
            _ => throw new SignatureBaseException(identifier, $"Unknown derived component '{identifier.Name}'."),
        };
    }

    private static string ResolveMethod(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@method is only valid for request messages.");

        // RFC 9421 §2.2.1: the method is taken verbatim, without any case normalization.
        return context.Method
            ?? throw new SignatureBaseException(identifier, "HTTP method is not available.");
    }

    private static string ResolveTargetUri(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@target-uri is only valid for request messages.");

        return context.TargetUri
            ?? throw new SignatureBaseException(identifier, "Target URI is not available.");
    }

    private static string ResolveAuthority(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@authority is only valid for request messages.");

        var authority = context.Authority
            ?? throw new SignatureBaseException(identifier, "Authority is not available.");

        // RFC 9421 §2.2.3: authority must be lowercase
        return authority.ToLowerInvariant();
    }

    private static string ResolveScheme(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@scheme is only valid for request messages.");

        var scheme = context.Scheme
            ?? throw new SignatureBaseException(identifier, "Scheme is not available.");

        // RFC 9421 §2.2.4: scheme must be lowercase
        return scheme.ToLowerInvariant();
    }

    private static string ResolveRequestTarget(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@request-target is only valid for request messages.");

        return context.RequestTarget
            ?? throw new SignatureBaseException(identifier, "Request target is not available.");
    }

    private static string ResolvePath(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@path is only valid for request messages.");

        var path = context.Path;

        // RFC 9421 §2.2.6: if the path is empty, use "/"
        if (string.IsNullOrEmpty(path))
            return "/";

        return path;
    }

    private static string ResolveQuery(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@query is only valid for request messages.");

        // RFC 9421 §2.2.7: query string including the leading '?'
        // If no query, use just "?"
        var query = context.Query;
        return query ?? "?";
    }

    private static string ResolveQueryParam(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (!context.IsRequest)
            throw new SignatureBaseException(identifier, "@query-param is only valid for request messages.");

        // ComponentValidator guarantees 'name' is present for '@query-param'.
        var paramName = identifier.QueryParamName!;

        var query = context.Query;
        if (query is null || query == "?")
            throw new SignatureBaseException(identifier, $"Query parameter '{paramName}' not found: no query string present.");

        // Remove leading '?'
        var queryString = query.StartsWith('?') ? query[1..] : query;

        var pairs = queryString.Length == 0 ? [] : queryString.Split('&');
        string? canonicalValue = null;
        var matchCount = 0;

        foreach (var pair in pairs)
        {
            var eqIdx = pair.IndexOf('=');
            string rawPairName;
            string rawPairValue;

            if (eqIdx >= 0)
            {
                rawPairName = pair[..eqIdx];
                rawPairValue = pair[(eqIdx + 1)..];
            }
            else
            {
                rawPairName = pair;
                rawPairValue = string.Empty;
            }

            // RFC 9421 §2.2.8 steps 1-2: parse (percent-decode, '+' as space) then re-encode via
            // the "percent-encode after encoding" process, for both the name and the value. The
            // 'name' parameter on the identifier is required to already be in this same canonical
            // encoded form, so it is compared directly rather than being decoded itself.
            string canonicalPairName;
            try
            {
                canonicalPairName = FormUrlEncoding.Encode(FormUrlEncoding.Decode(rawPairName));
            }
            catch (FormatException ex)
            {
                throw new SignatureBaseException(
                    identifier,
                    $"Query string parameter name could not be parsed as application/x-www-form-urlencoded: {ex.Message}",
                    ex);
            }

            if (canonicalPairName != paramName)
                continue;

            matchCount++;

            try
            {
                canonicalValue = FormUrlEncoding.Encode(FormUrlEncoding.Decode(rawPairValue));
            }
            catch (FormatException ex)
            {
                throw new SignatureBaseException(
                    identifier,
                    $"Query parameter '{paramName}' value could not be parsed as application/x-www-form-urlencoded: {ex.Message}",
                    ex);
            }
        }

        // RFC 9421 §2.2.8: a query parameter name that matches more than once cannot be
        // unambiguously covered, so it MUST NOT be included rather than silently picking one.
        if (matchCount > 1)
        {
            throw new SignatureBaseException(
                identifier,
                $"Query parameter '{paramName}' matches more than once in the query string and cannot be unambiguously covered.");
        }

        if (matchCount == 0)
            throw new SignatureBaseException(identifier, $"Query parameter '{paramName}' not found in query string.");

        // RFC 9421 §2.2.8: named parameters with an empty valueString have an empty component value.
        return canonicalValue ?? string.Empty;
    }

    private static string ResolveStatus(ComponentIdentifier identifier, IHttpMessageContext context)
    {
        if (context.IsRequest)
            throw new SignatureBaseException(identifier, "@status is only valid for response messages.");

        var status = context.StatusCode
            ?? throw new SignatureBaseException(identifier, "HTTP status code is not available.");

        return status.ToString(CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Implements the "application/x-www-form-urlencoded" decode/encode algorithms referenced by
/// RFC 9421 §2.2.8 for <c>@query-param</c> name matching, as defined by the WHATWG URL Standard's
/// form-urlencoded serializer/parser (not the same rules as general percent-encoding).
/// </summary>
internal static class FormUrlEncoding
{
    private const string UnreservedChars = "-._*";

    /// <summary>
    /// Decodes a raw query-string name or value using the form-urlencoded parser: '+' is
    /// replaced with a space (before percent-decoding), then '%XX' escapes are percent-decoded,
    /// and the resulting bytes are UTF-8 decoded. A raw (unescaped) non-ASCII character is
    /// rejected rather than silently accepted, since a query string is expected to already be
    /// percent-encoded. A stray '%' not followed by two hex digits is passed through literally,
    /// matching the permissive WHATWG form-urlencoded parser.
    /// </summary>
    internal static string Decode(string encoded)
    {
        ArgumentNullException.ThrowIfNull(encoded);

        var bytes = new List<byte>(encoded.Length);
        for (var i = 0; i < encoded.Length; i++)
        {
            var c = encoded[i];
            if (c == '+')
            {
                bytes.Add((byte)' ');
            }
            else if (c == '%' && i + 2 < encoded.Length && IsHexDigit(encoded[i + 1]) && IsHexDigit(encoded[i + 2]))
            {
                bytes.Add(Convert.ToByte(encoded.Substring(i + 1, 2), 16));
                i += 2;
            }
            else if (c <= 0x7F)
            {
                bytes.Add((byte)c);
            }
            else
            {
                throw new FormatException(
                    $"Query string contains raw character U+{(int)c:X4} that is not percent-encoded.");
            }
        }

        return System.Text.Encoding.UTF8.GetString([.. bytes]);
    }

    private static bool IsHexDigit(char c) => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    /// <summary>
    /// Encodes a decoded string using the form-urlencoded serializer: UTF-8 encode, then
    /// percent-encode every byte except ASCII letters, digits, '-', '.', '_', and '*'
    /// (notably, space is encoded as <c>%20</c>, not '+').
    /// </summary>
    internal static string Encode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var sb = new System.Text.StringBuilder(bytes.Length);

        foreach (var b in bytes)
        {
            var c = (char)b;
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || UnreservedChars.Contains(c))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return sb.ToString();
    }
}
