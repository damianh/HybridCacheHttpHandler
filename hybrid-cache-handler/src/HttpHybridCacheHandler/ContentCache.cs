// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers;
using Microsoft.Extensions.Caching.Hybrid;

namespace DamianH.HttpHybridCacheHandler;

/// <summary>
/// HybridCache-backed store for cached HTTP response content.
/// </summary>
internal sealed class ContentCache(HybridCache cache) : IHttpCacheContentStore
{
    public async ValueTask WriteAsync(
        string contentKey,
        ReadOnlySequence<byte> content,
        IEnumerable<string>? tags,
        Ct ct)
    {
        var payload = content.IsSingleSegment
            ? content.First.ToArray()
            : content.ToArray();

        // Store content as byte[] in HybridCache.
        await cache.SetAsync(contentKey, payload, tags: tags, cancellationToken: ct);
    }

    public async ValueTask<Stream?> OpenReadAsync(string contentKey, Ct ct)
    {
        var payload = await cache.GetOrCreateAsync<byte[]?>(
            contentKey,
            _ => ValueTask.FromResult<byte[]?>(null),
            cancellationToken: ct
        );
        return payload == null ? null : new MemoryStream(payload, writable: false);
    }

    public async ValueTask RemoveAsync(string contentKey, Ct ct) =>
        await cache.RemoveAsync(contentKey, ct);
}
