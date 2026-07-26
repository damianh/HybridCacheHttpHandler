// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers;
using Stowage;

namespace DamianH.HttpHybridCacheHandler.ContentStore.Stowage;

/// <summary>
/// Stowage-backed implementation of <see cref="ILargeHttpCacheContentStore"/>.
/// </summary>
public sealed class StowageLargeContentStore(IFileStorage fileStorage) : ILargeHttpCacheContentStore
{
    /// <inheritdoc />
    public async ValueTask WriteAsync(
        string contentKey,
        ReadOnlySequence<byte> content,
        IEnumerable<string>? tags,
        CancellationToken ct)
    {
        await using var destination = await fileStorage.OpenWrite(NormalizePath(contentKey), ct);
        if (content.IsSingleSegment)
        {
            await destination.WriteAsync(content.First, ct);
            return;
        }

        foreach (var segment in content)
        {
            if (segment.IsEmpty)
            {
                continue;
            }

            await destination.WriteAsync(segment, ct);
        }
    }

    /// <inheritdoc />
    public async ValueTask<Stream?> OpenReadAsync(string contentKey, CancellationToken ct) =>
        await fileStorage.OpenRead(NormalizePath(contentKey), ct);

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string contentKey, CancellationToken ct) =>
        await fileStorage.Rm(NormalizePath(contentKey), ct);

    private static string NormalizePath(string contentKey) =>
        contentKey.Replace(':', '/');
}
