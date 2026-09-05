// Copyright (c) Damian Hickey. All rights reserved.
// See LICENSE in the project root for license information.

using DamianH.Http.ForwardedHeaders;
using Microsoft.AspNetCore.Http.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddForwarded(options =>
{
    options.Parameters = ForwardedParameters.For | ForwardedParameters.Host
        | ForwardedParameters.Proto | ForwardedParameters.PathBase;
    options.ForwardLimit = 1;
    // Keep the loopback defaults: KnownProxies contains ::1 and KnownIPNetworks contains 127.0.0.0/8.
    // Only use this configuration when the proxy is local and direct client access is controlled.
    options.AllowedHosts.Add("localhost");
});

var app = builder.Build();

app.UseForwarded();
app.UseRouting();

app.MapGet("/request", (HttpContext context) => Results.Json(new
{
    url = context.Request.GetEncodedUrl(),
    remoteIpAddress = context.Connection.RemoteIpAddress?.ToString(),
    remotePort = context.Connection.RemotePort
}));

app.Run();
