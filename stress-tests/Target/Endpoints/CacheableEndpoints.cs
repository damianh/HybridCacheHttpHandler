using Target.Services;

namespace Target.Endpoints;

public static class CacheableEndpoints
{
    public static void MapCacheableEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api");

        // Basic cacheable endpoint
        // GET /api/cacheable/{id}?size=1024&delay=0
        group.MapGet("/cacheable/{id}", async (
            ResponseGenerator generator,
            int id,
            int size = 1024,
            int delay = 0) =>
        {
            if (delay > 0)
            {
                await Task.Delay(delay);
            }

            var content = generator.GenerateCompressible(size);

            return Results.Bytes(
                content,
                contentType: "application/json",
                enableRangeProcessing: false);
        })
        .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(60)));

        // No-cache endpoint
        // GET /api/nocache
        group.MapGet("/nocache", (ResponseGenerator generator) =>
        {
            var content = generator.GenerateJson(1024);
            return Results.Text(content, "application/json");
        })
        .CacheOutput(policy => policy.NoCache());

        // No-store endpoint
        // GET /api/nostore
        group.MapGet("/nostore", (HttpContext context, ResponseGenerator generator) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var content = generator.GenerateJson(1024);
            return Results.Text(content, "application/json");
        });

        // Stale-while-revalidate
        // GET /api/stale-while-revalidate
        group.MapGet("/stale-while-revalidate", (
            HttpContext context,
            ResponseGenerator generator) =>
        {
            var responseHeaders = context.Response.GetTypedHeaders();
            responseHeaders.CacheControl = new Microsoft.Net.Http.Headers.CacheControlHeaderValue
            {
                MaxAge = TimeSpan.FromSeconds(10),
            };
            context.Response.Headers.CacheControl = "max-age=10, stale-while-revalidate=30";
            var content = generator.GenerateJson(1024);
            return Results.Text(content, "application/json");
        });

        // Stale-if-error
        // GET /api/stale-if-error?fail=false
        group.MapGet("/stale-if-error", (
            HttpContext context,
            ResponseGenerator generator,
            bool fail = false) =>
        {
            context.Response.Headers.CacheControl = "max-age=10, stale-if-error=60";
            
            if (fail)
            {
                return Results.StatusCode(500);
            }

            var content = generator.GenerateJson(1024);
            return Results.Text(content, "application/json");
        });

        // Different sizes
        // GET /api/sizes/small
        group.MapGet("/sizes/small", (ResponseGenerator generator) =>
        {
            var content = generator.GenerateCompressible(1024); // 1KB
            return Results.Bytes(content, "application/json");
        })
        .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(60)));

        group.MapGet("/sizes/medium", (ResponseGenerator generator) =>
        {
            var content = generator.GenerateCompressible(100 * 1024); // 100KB
            return Results.Bytes(content, "application/json");
        })
        .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(60)));

        group.MapGet("/sizes/large", (ResponseGenerator generator) =>
        {
            var content = generator.GenerateCompressible(5 * 1024 * 1024); // 5MB
            return Results.Bytes(content, "application/json");
        })
        .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(60)));

        // Delay endpoint for stampede testing
        // GET /api/delay/{milliseconds}
        group.MapGet("/delay/{milliseconds}", async (
            int milliseconds,
            ResponseGenerator generator) =>
        {
            await Task.Delay(milliseconds);
            var content = generator.GenerateJson(1024);
            return Results.Text(content, "application/json");
        })
        .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(60)));
    }
}
