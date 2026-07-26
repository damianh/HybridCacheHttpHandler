// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers;

namespace DamianH.HttpHybridCacheHandler;

/// <summary>
/// Stores and retrieves cached HTTP response content bodies.
/// </summary>
public interface IHttpCacheContentStore
{
    /// <summary>
    /// Writes content for a cache key.
    /// </summary>
    ValueTask WriteAsync(
        string contentKey,
        ReadOnlySequence<byte> content,
        IEnumerable<string>? tags,
        Ct ct);

    /// <summary>
    /// Opens cached content for reading.
    /// Returns null when the content is missing.
    /// </summary>
    ValueTask<Stream?> OpenReadAsync(string contentKey, Ct ct);

    /// <summary>
    /// Removes cached content by key.
    /// </summary>
    ValueTask RemoveAsync(string contentKey, Ct ct);
}
