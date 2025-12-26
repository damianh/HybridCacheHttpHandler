var builder = DistributedApplication.CreateBuilder(args);

// Redis container for L2 cache
var redis = builder.AddRedis("redis")
    .WithRedisCommander();

// Target web application
var targetApp = builder.AddProject<Projects.Target>("target")
    .WithHttpEndpoint(port: 5001, name: "target-http")
    .WithHttpsEndpoint(port: 5002, name: "target-https");

// Runner console application (interactive stress tests)
var runner = builder.AddProject<Projects.Runner>("runner")
    .WithReference(redis)
    .WithEnvironment("TARGET_URL", targetApp.GetEndpoint("target-http"));

builder.Build().Run();
