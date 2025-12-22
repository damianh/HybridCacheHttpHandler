using Target.Services;

namespace Target.Endpoints;

public static class VaryEndpoints
{
    public static void MapVaryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/vary");

        // Vary by Accept
        // GET /api/vary/accept
        group.MapGet("/accept", (HttpContext context, ResponseGenerator generator) =>
        {
            context.Response.Headers.Vary = "Accept";
            context.Response.Headers.CacheControl = "public, max-age=60";

            var accept = context.Request.Headers.Accept.ToString();

            if (accept.Contains("application/json"))
            {
                var content = generator.GenerateJson(1024);
                return Results.Text(content, "application/json");
            }
            else if (accept.Contains("text/html"))
            {
                var content = "<html><body>HTML Response</body></html>";
                return Results.Text(content, "text/html");
            }
            else
            {
                return Results.Text("Plain text response", "text/plain");
            }
        });

        // Vary by Accept-Encoding
        // GET /api/vary/encoding
        group.MapGet("/encoding", (HttpContext context, ResponseGenerator generator) =>
        {
            context.Response.Headers.Vary = "Accept-Encoding";
            context.Response.Headers.CacheControl = "public, max-age=60";

            var encoding = context.Request.Headers.AcceptEncoding.ToString();
            var content = generator.GenerateJson(1024);
            
            return Results.Text($"{{\"encoding\": \"{encoding}\", \"content\": {content}}}", "application/json");
        });

        // Vary by Accept-Language
        // GET /api/vary/language
        group.MapGet("/language", (HttpContext context, ResponseGenerator generator) =>
        {
            context.Response.Headers.Vary = "Accept-Language";
            context.Response.Headers.CacheControl = "public, max-age=60";

            var language = context.Request.Headers.AcceptLanguage.ToString();
            var content = generator.GenerateJson(1024);
            
            return Results.Text($"{{\"language\": \"{language}\", \"content\": {content}}}", "application/json");
        });

        // Vary by multiple headers
        // GET /api/vary/multiple
        group.MapGet("/multiple", (HttpContext context, ResponseGenerator generator) =>
        {
            context.Response.Headers.Vary = "Accept, Accept-Encoding, Accept-Language";
            context.Response.Headers.CacheControl = "public, max-age=60";

            var accept = context.Request.Headers.Accept.ToString();
            var encoding = context.Request.Headers.AcceptEncoding.ToString();
            var language = context.Request.Headers.AcceptLanguage.ToString();

            var content = generator.GenerateJson(512);
            var response = $$"""
            {
                "accept": "{{accept}}",
                "encoding": "{{encoding}}",
                "language": "{{language}}",
                "content": {{content}}
            }
            """;
            
            return Results.Text(response, "application/json");
        });
    }
}
