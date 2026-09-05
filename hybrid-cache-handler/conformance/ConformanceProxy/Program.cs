// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

// Reverse proxy wrapping HttpHybridCacheHandler for the http-tests/cache-tests
// RFC 9111 conformance suite (https://github.com/http-tests/cache-tests).
// The suite's client sends requests through this proxy to the suite's origin server.

using System.Diagnostics;
using System.Net;
using DamianH.HttpHybridCacheHandler;
using DamianH.HttpHybridCacheHandler.ContentStore.FileSystem;
using Microsoft.Extensions.Caching.Hybrid;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);

var port = int.TryParse(builder.Configuration["port"], out var p) ? p : 8081;
var origin = builder.Configuration["origin"] ?? "http://127.0.0.1:8000";

builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, port));
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddHybridCache();
builder.Services.AddHttpForwarder();
var useFileSystem = bool.TryParse(builder.Configuration["file-system"], out var enabled) && enabled;
if (useFileSystem)
{
    var root = builder.Configuration["content-root"]
        ?? throw new InvalidOperationException("--content-root is required in filesystem mode.");
    builder.Services.AddHttpHybridCacheFileSystemContentStore(store =>
    {
        store.RootDirectory = root;
        store.MaximumAge = TimeSpan.FromDays(1);
        store.MaximumTotalBytes = 1024L * 1024 * 1024;
    });
}

var app = builder.Build();

// Shared (proxy) cache mode; fallback/default caching stays disabled because
// heuristic "default caching" interferes with the suite (see its README).
var options = new HttpHybridCacheHandlerOptions
{
    Mode = CacheMode.Shared,
    MaxCacheableContentSize = 50 * 1024 * 1024,
    LargeContentThreshold = useFileSystem ? 1 : 1024 * 1024,
    VaryHeaders = []
};

var socketsHandler = new SocketsHttpHandler
{
    UseProxy = false,
    AllowAutoRedirect = false,
    AutomaticDecompression = DecompressionMethods.None,
    UseCookies = false,
    ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current)
};

var cachingHandler = new HttpHybridCacheHandler(
    socketsHandler,
    app.Services.GetRequiredService<HybridCache>(),
    TimeProvider.System,
    contentStore: null,
    options,
    app.Services.GetRequiredService<ILogger<HttpHybridCacheHandler>>(),
    app.Services.GetService<ILargeHttpCacheContentStore>());

var invoker = new HttpMessageInvoker(cachingHandler, disposeHandler: false);
var forwarder = app.Services.GetRequiredService<IHttpForwarder>();
var requestConfig = new ForwarderRequestConfig
{
    Version = HttpVersion.Version11,
    VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
};

app.Map("/proxy-health", () => Results.Ok("OK"));

app.Map("/{**catch-all}", async httpContext =>
{
    var error = await forwarder.SendAsync(httpContext, origin, invoker, requestConfig);
    if (error != ForwarderError.None)
    {
        var errorFeature = httpContext.GetForwarderErrorFeature();
        app.Logger.LogWarning(errorFeature?.Exception, "Forwarding error: {Error}", error);
    }
});

app.Logger.LogWarning("ConformanceProxy listening on http://127.0.0.1:{Port}, forwarding to {Origin}", port, origin);

app.Run();
