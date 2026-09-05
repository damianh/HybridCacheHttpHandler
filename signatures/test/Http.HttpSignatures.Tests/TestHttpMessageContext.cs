// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.HttpSignatures;

/// <summary>
/// A simple in-memory <see cref="IHttpMessageContext"/> implementation for testing.
/// Allows manual construction of request/response messages with full control over all properties.
/// </summary>
internal sealed class TestHttpMessageContext : IHttpMessageContext
{
    private readonly Dictionary<string, List<string>> _headers =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<string>> _trailers =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public bool IsRequest { get; set; } = true;

    /// <inheritdoc/>
    public string? Method { get; set; }

    /// <inheritdoc/>
    public string? Scheme { get; set; }

    /// <inheritdoc/>
    public string? Authority { get; set; }

    /// <inheritdoc/>
    public string? Path { get; set; }

    /// <inheritdoc/>
    public string? Query { get; set; }

    /// <inheritdoc/>
    public string? TargetUri { get; set; }

    /// <inheritdoc/>
    public string? RequestTarget { get; set; }

    /// <inheritdoc/>
    public int? StatusCode { get; set; }

    /// <inheritdoc/>
    public IHttpMessageContext? AssociatedRequest { get; set; }

    /// <summary>
    /// Adds a header value. Multiple calls with the same name add multiple values.
    /// </summary>
    public void AddHeader(string name, string value)
    {
        if (!_headers.TryGetValue(name, out var values))
        {
            values = new List<string>();
            _headers[name] = values;
        }
        values.Add(value);
    }

    /// <summary>
    /// Sets a header to a single value, replacing any existing values.
    /// </summary>
    public void SetHeader(string name, string value)
    {
        _headers[name] = [value];
    }

    /// <summary>
    /// Adds a trailer value. Multiple calls with the same name add multiple values.
    /// </summary>
    public void AddTrailer(string name, string value)
    {
        if (!_trailers.TryGetValue(name, out var values))
        {
            values = [];
            _trailers[name] = values;
        }
        values.Add(value);
    }

    /// <summary>
    /// Sets a trailer to a single value, replacing any existing values.
    /// </summary>
    public void SetTrailer(string name, string value)
    {
        _trailers[name] = [value];
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetHeaderValues(string fieldName)
    {
        if (!_headers.TryGetValue(fieldName, out var values))
            return [];
        return values.AsReadOnly();
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetTrailerValues(string fieldName)
    {
        if (!_trailers.TryGetValue(fieldName, out var values))
            return [];
        return values.AsReadOnly();
    }

    /// <summary>
    /// Creates a test request context from common fields.
    /// </summary>
    public static TestHttpMessageContext CreateRequest(
        string method,
        string scheme,
        string authority,
        string path,
        string? query = null)
    {
        var uri = $"{scheme}://{authority}{path}{query}";
        var requestTarget = query is not null ? $"{path}{query}" : path;

        return new TestHttpMessageContext
        {
            IsRequest = true,
            Method = method,
            Scheme = scheme,
            Authority = authority,
            Path = path,
            Query = query,
            TargetUri = uri,
            RequestTarget = requestTarget,
        };
    }

    /// <summary>
    /// Creates a test response context.
    /// </summary>
    public static TestHttpMessageContext CreateResponse(
        int statusCode,
        TestHttpMessageContext? associatedRequest = null) =>
        new()
        {
            IsRequest = false,
            StatusCode = statusCode,
            AssociatedRequest = associatedRequest,
        };
}
