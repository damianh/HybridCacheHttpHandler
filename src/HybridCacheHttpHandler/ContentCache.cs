// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Hybrid;

namespace DamianH.HybridCacheHttpHandler;

/// <summary>
/// Manages caching of HTTP response content separately from metadata.
/// Content is stored as raw byte arrays to avoid Base64 encoding overhead.
/// </summary>
/// <remarks>
/// Large content (>85KB) will be allocated on the Large Object Heap.
/// This is acceptable for reliability-focused scenarios where caching reduces load on target systems.
/// Compression (enabled by default) often reduces content size below LOH threshold.
/// </remarks>
internal sealed class ContentCache(HybridCache cache, string contentKeyPrefix)
{
    /// <summary>
    /// Stores content in cache and returns its key.
    /// Uses content hash for deduplication.
    /// </summary>
    public async Task<string> StoreContentAsync(byte[] content, Ct ct)
    {
        var hash = ComputeContentHash(content);
        var contentKey = $"{contentKeyPrefix}{hash}";

        // Store content as byte[] - no serialization overhead!
        // HybridCache treats byte[] as a primitive type and stores it directly
        await cache.SetAsync(contentKey, content, cancellationToken: ct);

        return contentKey;
    }

    /// <summary>
    /// Retrieves content from cache by key.
    /// Returns null if content is not found.
    /// </summary>
    public async Task<byte[]?> GetContentAsync(string contentKey, Ct ct) =>
        await cache.GetOrCreateAsync<byte[]?>(
            contentKey,
            _ => ValueTask.FromResult<byte[]?>(null),
            cancellationToken: ct
        );

    /// <summary>
    /// Removes content from cache.
    /// Used for cleanup of orphaned content.
    /// </summary>
    public async Task RemoveContentAsync(string contentKey, Ct ct) =>
        await cache.RemoveAsync(contentKey, ct);

    /// <summary>
    /// Computes a hash of the content for use as a cache key.
    /// Uses SHA256 for reliable content-based addressing.
    /// </summary>
    private static string ComputeContentHash(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content));
}
