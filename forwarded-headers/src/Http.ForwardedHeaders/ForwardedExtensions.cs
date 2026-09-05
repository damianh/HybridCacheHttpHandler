// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DamianH.Http.ForwardedHeaders;

/// <summary>Registers RFC 7239 middleware, separately from X-Forwarded-* middleware.</summary>
public static class ForwardedExtensions
{
    /// <summary>Adds and configures RFC 7239 options, validating them at host startup.</summary>
    public static IServiceCollection AddForwarded(this IServiceCollection services, Action<ForwardedOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        services.AddOptions<ForwardedOptions>()
            .Configure(configure)
            .Validate(options =>
            {
                _ = new ForwardedSettings(options);
                return true;
            })
            .ValidateOnStart();
        return services;
    }

    /// <summary>Uses configured RFC 7239 forwarding. Register before redirects, authentication, and endpoints.</summary>
    public static IApplicationBuilder UseForwarded(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ForwardedMiddleware>();
    }

    /// <summary>Uses explicit RFC 7239 options, validated when the pipeline is built.</summary>
    public static IApplicationBuilder UseForwarded(this IApplicationBuilder app, ForwardedOptions options)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(options);
        return app.UseMiddleware<ForwardedMiddleware>(Options.Create(options));
    }
}
