using Target.Services;

namespace Target.Endpoints;

public static class ConditionalEndpoints
{
    private static readonly Dictionary<string, (string etag, DateTimeOffset lastModified, string content)> ContentStore = new();

    public static void MapConditionalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/conditional");

        // ETag support
        // GET /api/conditional/etag/{id}
        group.MapGet("/etag/{id}", (HttpContext context, string id, ResponseGenerator generator) =>
        {
            // Generate or retrieve content
            if (!ContentStore.TryGetValue(id, out var stored))
            {
                var content = generator.GenerateJson(1024);
                var etag = $"\"{Guid.NewGuid():N}\"";
                var lastModified = DateTimeOffset.UtcNow;
                
                stored = (etag, lastModified, content);
                ContentStore[id] = stored;
            }

            context.Response.Headers.ETag = stored.etag;
            context.Response.Headers.CacheControl = "public, max-age=60";

            // Check If-None-Match
            if (context.Request.Headers.IfNoneMatch == stored.etag)
            {
                return Results.StatusCode(304); // Not Modified
            }

            return Results.Text(stored.content, "application/json");
        });

        // Last-Modified support
        // GET /api/conditional/lastmodified/{id}
        group.MapGet("/lastmodified/{id}", (HttpContext context, string id, ResponseGenerator generator) =>
        {
            // Generate or retrieve content
            if (!ContentStore.TryGetValue(id, out var stored))
            {
                var content = generator.GenerateJson(1024);
                var etag = $"\"{Guid.NewGuid():N}\"";
                var lastModified = DateTimeOffset.UtcNow;
                
                stored = (etag, lastModified, content);
                ContentStore[id] = stored;
            }

            context.Response.Headers.LastModified = stored.lastModified.ToString("R");
            context.Response.Headers.CacheControl = "public, max-age=60";

            // Check If-Modified-Since
            if (context.Request.Headers.IfModifiedSince.Count > 0 &&
                DateTimeOffset.TryParse(context.Request.Headers.IfModifiedSince.ToString(), out var ifModifiedSince) &&
                stored.lastModified <= ifModifiedSince)
            {
                return Results.StatusCode(304); // Not Modified
            }

            return Results.Text(stored.content, "application/json");
        });

        // Both ETag and Last-Modified
        // GET /api/conditional/both/{id}
        group.MapGet("/both/{id}", (HttpContext context, string id, ResponseGenerator generator) =>
        {
            // Generate or retrieve content
            if (!ContentStore.TryGetValue(id, out var stored))
            {
                var content = generator.GenerateJson(1024);
                var etag = $"\"{Guid.NewGuid():N}\"";
                var lastModified = DateTimeOffset.UtcNow;
                
                stored = (etag, lastModified, content);
                ContentStore[id] = stored;
            }

            context.Response.Headers.ETag = stored.etag;
            context.Response.Headers.LastModified = stored.lastModified.ToString("R");
            context.Response.Headers.CacheControl = "public, max-age=60";

            // Check both conditions (ETag takes precedence)
            var hasIfNoneMatch = context.Request.Headers.IfNoneMatch.Count > 0;
            var hasIfModifiedSince = context.Request.Headers.IfModifiedSince.Count > 0;

            if (hasIfNoneMatch && context.Request.Headers.IfNoneMatch == stored.etag)
            {
                return Results.StatusCode(304);
            }

            if (!hasIfNoneMatch && hasIfModifiedSince &&
                DateTimeOffset.TryParse(context.Request.Headers.IfModifiedSince.ToString(), out var ifModifiedSince) &&
                stored.lastModified <= ifModifiedSince)
            {
                return Results.StatusCode(304);
            }

            return Results.Text(stored.content, "application/json");
        });
    }
}
