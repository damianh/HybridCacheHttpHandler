// ASP.NET Core Sample demonstrating HTTP Structured Field Values usage

using AspNetCoreSample;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Endpoint demonstrating parsing typed headers from requests
app.MapGet("/parse-headers", (HttpRequest request) =>
{
    var results = new Dictionary<string, object>();

    // Parse Priority as a typed header
    if (request.TryGetHeader("Priority", PriorityHeader.Mapper, out var priority))
    {
        results["Priority"] = new
        {
            priority.Urgency,
            priority.Incremental
        };
    }

    // Parse Accept-CH as a typed header
    if (request.TryGetHeader("Accept-CH", AcceptClientHintHeaderValue.Mapper, out var acceptCH))
    {
        results["Accept-CH"] = acceptCH.Hints;
    }

    return Results.Ok(new { ParsedHeaders = results });
});

// Endpoint demonstrating setting typed headers on responses
app.MapGet("/set-headers", (HttpResponse response) =>
{
    // Set Priority using the typed mapper
    var priority = new PriorityHeader { Urgency = 3, Incremental = true };
    response.SetHeader("Priority", PriorityHeader.Mapper, priority);

    // Set Accept-CH using the typed mapper
    var acceptCH = new AcceptClientHintHeaderValue
    {
        Hints = ["Sec-CH-UA", "Sec-CH-UA-Platform", "Sec-CH-UA-Mobile"]
    };
    response.SetHeader("Accept-CH", AcceptClientHintHeaderValue.Mapper, acceptCH);

    // Set a custom structured list header
    var features = new SupportedFeaturesHeader
    {
        Features = ["feature-a", "feature-b", "feature-c"]
    };
    response.SetHeader("X-Supported-Features", SupportedFeaturesHeader.Mapper, features);

    return Results.Ok(new { Message = "Structured field headers set on response" });
});

// Endpoint demonstrating the Accept-CH header value type
app.MapGet("/client-hints", (HttpRequest request, HttpResponse response) =>
{
    var acceptCH = new AcceptClientHintHeaderValue
    {
        Hints = ["Sec-CH-UA", "Sec-CH-UA-Platform", "Sec-CH-UA-Mobile", "Sec-CH-UA-Full-Version-List"]
    };

    response.SetHeader("Accept-CH", AcceptClientHintHeaderValue.Mapper, acceptCH);

    var serialized = AcceptClientHintHeaderValue.Mapper.Serialize(acceptCH);

    // Parse the Accept-CH header back (for demonstration)
    if (AcceptClientHintHeaderValue.Mapper.TryParse(serialized, out var parsed))
    {
        return Results.Ok(new
        {
            AcceptCHHeader = serialized,
            RequestedHints = parsed.Hints,
            ContainsSecChUa = parsed.Contains("Sec-CH-UA"),
            ContainsDeviceMemory = parsed.Contains("Device-Memory")
        });
    }

    return Results.Ok(new { AcceptCHHeader = serialized });
});

// Root endpoint
app.MapGet("/", () => Results.Ok(new
{
    Message = "HTTP Structured Field Values - ASP.NET Core Sample",
    Endpoints = new[]
    {
        new { Path = "/parse-headers", Description = "Demonstrates parsing typed structured field headers from requests." },
        new { Path = "/set-headers", Description = "Demonstrates setting typed structured field headers on responses." },
        new { Path = "/client-hints", Description = "Demonstrates the Accept-CH header value type." }
    }
}));

app.Run();
