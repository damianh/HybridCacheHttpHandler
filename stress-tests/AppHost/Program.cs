var builder = DistributedApplication.CreateBuilder(args);

// Redis container for L2 cache
var redis = builder.AddRedis("redis")
    .WithRedisCommander(); // Optional: Redis Commander UI

// Target web application
var targetApp = builder.AddProject<Projects.Target>("target")
    .WithHttpEndpoint(port: 5001, name: "http")
    .WithHttpsEndpoint(port: 5002, name: "https");

// Console runner - interactive
// Gets Redis connection string via service discovery
_ = builder.AddProject<Projects.Runner>("runner")
    .WithReference(targetApp)
    .WithReference(redis)
    .WithEnvironment("TARGET_URL", targetApp.GetEndpoint("http"));

builder.Build().Run();
