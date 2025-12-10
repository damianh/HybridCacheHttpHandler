// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace DamianH.HybridCacheHttpHandler;

internal static class TestHelpers
{
    public static HybridCache CreateCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<HybridCache>();
    }

    public static FakeTimeProvider CreateTimeProvider() => new();
}
