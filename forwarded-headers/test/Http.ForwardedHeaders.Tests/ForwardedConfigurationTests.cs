// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace DamianH.Http.ForwardedHeaders;

public class ForwardedConfigurationTests
{
    [Fact]
    public void DefaultsMatchFrameworkPrinciples()
    {
        var options = new ForwardedOptions();
        options.Parameters.ShouldBe(ForwardedParameters.None);
        options.ForwardLimit.ShouldBe(1);
        options.KnownProxies.ShouldBe([IPAddress.IPv6Loopback]);
        options.KnownIPNetworks.ShouldBe([IPNetwork.Parse("127.0.0.0/8")]);
        options.AllowedHosts.ShouldBeEmpty();
        options.MalformedHeaderBehavior.ShouldBe(MalformedHeaderBehavior.Ignore);
        options.HeaderName.ShouldBe("Forwarded");
        (ForwardedParameters.All & ForwardedParameters.PathBase).ShouldBe(ForwardedParameters.None);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void NegativeForwardLimitsFailAtInitialization(int limit)
    {
        var options = new ForwardedOptions { ForwardLimit = limit };
        Should.Throw<ArgumentException>(() => ForwardedMiddlewareTests.Middleware(options));
    }

    [Fact]
    public void UnknownEnumsFailAtInitialization()
    {
        Should.Throw<ArgumentException>(() => ForwardedMiddlewareTests.Middleware(new ForwardedOptions
        {
            Parameters = (ForwardedParameters)128
        }));
        Should.Throw<ArgumentException>(() => ForwardedMiddlewareTests.Middleware(new ForwardedOptions
        {
            MalformedHeaderBehavior = (MalformedHeaderBehavior)999
        }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad header")]
    [InlineData("bad\r\nheader")]
    [InlineData("X-Original-For")]
    [InlineData("x-original-host")]
    [InlineData(null)]
    public void InvalidOrCollidingHeaderNamesFailAtInitialization(string? header)
    {
        var options = new ForwardedOptions { HeaderName = header! };
        Should.Throw<ArgumentException>(() => ForwardedMiddlewareTests.Middleware(options));
    }

    [Theory]
    [InlineData("")]
    [InlineData("example.com:443")]
    [InlineData("bad/host")]
    [InlineData("*.")]
    [InlineData("*.example.com:443")]
    public void InvalidAllowedHostsFailAtInitialization(string host)
    {
        var options = new ForwardedOptions();
        options.AllowedHosts.Add(host);
        Should.Throw<ArgumentException>(() => ForwardedMiddlewareTests.Middleware(options));
    }

    [Fact]
    public async Task OptionsAreSnapshottedRatherThanMutatedDuringRequests()
    {
        var options = ForwardedMiddlewareTests.Options();
        options.AllowedHosts.Add("example.com");
        var middleware = ForwardedMiddlewareTests.Middleware(options);
        options.Parameters = ForwardedParameters.None;
        options.AllowedHosts.Clear();
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();

        var trusted = ForwardedMiddlewareTests.Context("for=192.0.2.1;proto=https;host=example.com");
        await middleware.InvokeAsync(trusted);
        trusted.Request.Scheme.ShouldBe("https");

        var untrusted = ForwardedMiddlewareTests.Context("for=192.0.2.1;proto=https", "10.1.1.1");
        await middleware.InvokeAsync(untrusted);
        untrusted.Request.Scheme.ShouldBe("http");

        var badHost = ForwardedMiddlewareTests.Context("for=192.0.2.1;host=evil.example");
        await middleware.InvokeAsync(badHost);
        badHost.Request.Host.Value.ShouldBe("backend.internal:8080");
    }

    [Fact]
    public async Task FeatureReturnsCopiesOfMutableIpAddresses()
    {
        var context = ForwardedMiddlewareTests.Context("for=unknown;proto=https", "fe80::1%2");
        var options = ForwardedMiddlewareTests.Options();
        options.KnownProxies.Add(IPAddress.Parse("fe80::1%2"));
        await ForwardedMiddlewareTests.Middleware(options).InvokeAsync(context);
        var feature = context.Features.Get<IForwardedFeature>().ShouldNotBeNull();
        feature.OriginalRemoteIpAddress!.ScopeId = 3;
        feature.OriginalRemoteIpAddress!.ScopeId.ShouldBe(2);
    }

    [Fact]
    public async Task ExplicitHeaderNamesAreUsedForReadingAndOriginals()
    {
        var context = ForwardedMiddlewareTests.Context("for=198.51.100.1;proto=unused");
        context.Request.Headers["Custom-Forwarded"] = "for=192.0.2.1;proto=https;host=example.com;pathbase=\"/new\"";
        var options = ForwardedMiddlewareTests.Options();
        options.Parameters |= ForwardedParameters.PathBase;
        options.HeaderName = "Custom-Forwarded";
        options.OriginalForHeaderName = "Original-For";
        options.OriginalHostHeaderName = "Original-Host";
        options.OriginalProtoHeaderName = "Original-Proto";
        options.OriginalPrefixHeaderName = "Original-Prefix";
        await ForwardedMiddlewareTests.Middleware(options).InvokeAsync(context);
        context.Request.Headers["Original-For"].ToString().ShouldBe("127.0.0.1:5000");
        context.Request.Headers["Original-Host"].ToString().ShouldBe("backend.internal:8080");
        context.Request.Headers["Original-Proto"].ToString().ShouldBe("http");
        context.Request.Headers["Original-Prefix"].ToString().ShouldBe("/old");
        context.Request.Headers.ContainsKey("Custom-Forwarded").ShouldBeFalse();
        context.Request.Headers["Forwarded"].ToString().ShouldBe("for=198.51.100.1;proto=unused");
    }

    [Fact]
    public async Task RegisteredMiddlewareRunsBeforeEndpointsAndUrlGeneration()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddForwarded(options =>
        {
            options.Parameters = ForwardedParameters.All | ForwardedParameters.PathBase;
            options.AllowedHosts.Add("example.com");
        });
        await using var app = builder.Build();
        string? url = null;
        string? link = null;
        app.UseForwarded();
        app.UseRouting();
        app.MapGet("/resource", (HttpContext context) =>
        {
            url = context.Request.GetEncodedUrl();
            link = context.RequestServices.GetRequiredService<LinkGenerator>()
                .GetUriByName(context, "resource", values: null);
            return Task.CompletedTask;
        }).WithName("resource");
        app.UseEndpoints(_ => { });

        var pipeline = ((IApplicationBuilder)app).Build();
        var context = ForwardedMiddlewareTests.Context("for=192.0.2.1;proto=https;host=\"example.com:8443\";pathbase=\"/public\"");
        context.Request.Method = "GET";
        context.RequestServices = app.Services;
        await pipeline(context);

        url.ShouldBe("https://example.com:8443/public/resource");
        link.ShouldBe("https://example.com:8443/public/resource");
        context.Request.Path.Value.ShouldBe("/resource");
    }

    [Fact]
    public async Task ExplicitOptionsOverloadWorksWithoutAddForwarded()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);
        app.UseForwarded(new ForwardedOptions { Parameters = ForwardedParameters.Proto });
        var called = false;
        app.Run(_ => { called = true; return Task.CompletedTask; });
        var context = ForwardedMiddlewareTests.Context("proto=https");
        await app.Build()(context);
        called.ShouldBeTrue();
        context.Request.Scheme.ShouldBe("https");
    }

    [Fact]
    public void RegisteredOptionsValidateAtResolution()
    {
        var services = new ServiceCollection();
        services.AddForwarded(options => options.ForwardLimit = -1);
        using var provider = services.BuildServiceProvider();
        Should.Throw<ArgumentException>(() => provider.GetRequiredService<IOptions<ForwardedOptions>>().Value);
    }
}
