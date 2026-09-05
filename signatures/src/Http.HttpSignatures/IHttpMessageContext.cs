// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// Abstraction over an HTTP message providing component values for signature base construction.
/// Implementations adapt specific HTTP frameworks (HttpClient, ASP.NET Core, etc.).
/// </summary>
public interface IHttpMessageContext
{
    /// <summary>Whether this context represents a request or a response.</summary>
    bool IsRequest { get; }

    /// <summary>The HTTP method (e.g., "GET", "POST"). Only valid for requests.</summary>
    string? Method { get; }

    /// <summary>The scheme of the target URI (e.g., "https"). Only valid for requests.</summary>
    string? Scheme { get; }

    /// <summary>The authority of the target URI (e.g., "example.com"). Only valid for requests.</summary>
    string? Authority { get; }

    /// <summary>The absolute path of the target URI (e.g., "/foo"). Only valid for requests.</summary>
    string? Path { get; }

    /// <summary>The query string including leading '?' (e.g., "?a=b"). Empty query is "?". Absent query is null. Only valid for requests.</summary>
    string? Query { get; }

    /// <summary>The full target URI (e.g., "https://example.com/foo?a=b"). Only valid for requests.</summary>
    string? TargetUri { get; }

    /// <summary>The request target (e.g., "/foo?a=b"). Only valid for requests.</summary>
    string? RequestTarget { get; }

    /// <summary>The HTTP status code. Only valid for responses.</summary>
    int? StatusCode { get; }

    /// <summary>
    /// Gets the individual, uncombined raw values for the named header field, in field-line order.
    /// Returns empty when the header is not present. This is the sole authoritative source of header
    /// data: combination for ordinary field access and lossless octet wrapping for <c>bs</c> both
    /// build on these raw values via <see cref="HttpMessageContextExtensions"/>; implementations are
    /// not required to also provide an independently canonicalized combined-value API.
    /// </summary>
    IReadOnlyList<string> GetHeaderValues(string fieldName);

    /// <summary>
    /// Gets the individual, uncombined raw values for the named trailer field, in field-line order.
    /// Returns empty when the trailer is not present or trailers are not available. A missing trailer
    /// is never satisfied by falling back to a header of the same name, and the two sections are never
    /// combined. Implementations that do not support trailers should always return an empty list.
    /// </summary>
    IReadOnlyList<string> GetTrailerValues(string fieldName);

    /// <summary>
    /// For response contexts, the associated request context (for <c>req</c> parameter).
    /// Null for request contexts.
    /// </summary>
    IHttpMessageContext? AssociatedRequest { get; }
}
