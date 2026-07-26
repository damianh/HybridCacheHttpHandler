// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.HttpHybridCacheHandler.ContentStore.Stowage;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stowage;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Registers Stowage-backed large-content storage for HttpHybridCacheHandler.
/// </summary>
public static class StowageServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Stowage large-content store.
    /// Requires an <see cref="IFileStorage"/> registration in the container.
    /// </summary>
    public static IServiceCollection AddHttpHybridCacheStowageLargeContentStore(this IServiceCollection services)
    {
        services.AddHttpHybridCacheLargeContentStore<StowageLargeContentStore>();
        return services;
    }

    /// <summary>
    /// Registers an <see cref="IFileStorage"/> factory and the Stowage large-content store.
    /// </summary>
    public static IServiceCollection AddHttpHybridCacheStowageLargeContentStore(
        this IServiceCollection services,
        Func<IServiceProvider, IFileStorage> fileStorageFactory)
    {
        services.TryAddSingleton(fileStorageFactory);
        services.AddHttpHybridCacheLargeContentStore<StowageLargeContentStore>();
        return services;
    }
}
