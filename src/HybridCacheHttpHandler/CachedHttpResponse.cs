// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.HybridCacheHttpHandler;

/// <summary>
/// Cached HTTP response metadata without the content body.
/// Content is stored separately to avoid Base64 encoding overhead in distributed cache.
/// </summary>
internal sealed class CachedHttpMetadata
{
    public required int StatusCode { get; init; }
    /// <summary>
    /// Reference key for content stored separately.
    /// Format: "httpcache:content:{hash}"
    /// Content is always stored separately as raw bytes to avoid Base64 encoding overhead.
    /// </summary>
    public required string ContentKey { get; init; }
    public required long ContentLength { get; init; }
    public required Dictionary<string, string[]> Headers { get; init; }
    public required Dictionary<string, string[]> ContentHeaders { get; init; }
    public required DateTimeOffset CachedAt { get; init; }
    public TimeSpan? MaxAge { get; init; }
    public string? ETag { get; init; }
    public DateTimeOffset? LastModified { get; init; }
    public DateTimeOffset? Expires { get; init; }
    public DateTimeOffset? Date { get; init; }
    public TimeSpan? Age { get; init; }
    public string[]? VaryHeaders { get; init; }
    public Dictionary<string, string>? VaryHeaderValues { get; init; }
    public TimeSpan? StaleWhileRevalidate { get; init; }
    public TimeSpan? StaleIfError { get; init; }
    public bool MustRevalidate { get; init; }
    public bool NoCache { get; init; }
    public bool IsCompressed { get; init; }
}
