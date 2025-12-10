// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.HybridCacheHttpHandler;

internal sealed class CachedHttpResponse
{
    public required int StatusCode { get; init; }
    public required ReadOnlyMemory<byte> Content { get; init; }
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
