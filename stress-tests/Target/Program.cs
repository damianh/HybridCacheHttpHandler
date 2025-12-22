using Target.Endpoints;
using Target.Services;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddSingleton<ResponseGenerator>();

// Output caching
builder.Services.AddOutputCache();

var app = builder.Build();

app.UseOutputCache();

// Register endpoint groups
app.MapCacheableEndpoints();
app.MapVaryEndpoints();
app.MapConditionalEndpoints();

app.Run();
