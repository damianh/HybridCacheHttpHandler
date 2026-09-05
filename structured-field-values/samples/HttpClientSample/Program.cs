// HttpClient Sample demonstrating HTTP Structured Field Values usage

using DamianH.Http.StructuredFieldValues;
using HttpClientSample;

Console.WriteLine("HTTP Structured Field Values - HttpClient Sample");
Console.WriteLine("=".PadRight(50, '='));

using var httpClient = new HttpClient();

// Example 1: Set typed headers on a request
Console.WriteLine("\n1. Setting typed headers on a request:");

var request = new HttpRequestMessage(HttpMethod.Get, "https://httpbin.org/headers");

// Set a Priority header using the typed mapper
var priority = new PriorityHeader { Urgency = 1, Incremental = true };
request.SetHeader("Priority", PriorityHeader.Mapper, priority);

Console.WriteLine($"  Priority: {request.Headers.GetValues("Priority").First()}");

// Example 2: Parse typed headers from strings
Console.WriteLine("\n2. Parsing typed headers:");

var parsedPriority = PriorityHeader.Mapper.Parse("u=5, i");
Console.WriteLine($"  Parsed Priority:");
Console.WriteLine($"    Urgency: {parsedPriority.Urgency}");
Console.WriteLine($"    Incremental: {parsedPriority.Incremental}");

// Example 3: Round-trip with TryParse
Console.WriteLine("\n3. Round-trip demonstration:");

var original = new PriorityHeader { Urgency = 3, Incremental = true };
var serialized = PriorityHeader.Mapper.Serialize(original);
Console.WriteLine($"  Serialized: {serialized}");

if (PriorityHeader.Mapper.TryParse(serialized, out var parsed))
{
    Console.WriteLine($"  Parsed back - Urgency: {parsed.Urgency}, Incremental: {parsed.Incremental}");
}

// Example 4: Parse from response headers
Console.WriteLine("\n4. Using TryGetHeader pattern:");
var mockResponse = new HttpResponseMessage();
mockResponse.Headers.TryAddWithoutValidation("Priority", "u=2");

if (mockResponse.TryGetHeader("Priority", PriorityHeader.Mapper, out var responsePriority))
{
    Console.WriteLine($"  Response Priority:");
    Console.WriteLine($"    Urgency: {responsePriority.Urgency}");
    Console.WriteLine($"    Incremental: {responsePriority.Incremental}");
}

Console.WriteLine("\nSample completed successfully!");
