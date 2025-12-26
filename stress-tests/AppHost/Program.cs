var builder = DistributedApplication.CreateBuilder(args);

// Redis container for L2 cache
var redis = builder.AddRedis("redis")
    .WithRedisCommander();

// Target web application
var targetApp = builder.AddProject<Projects.Target>("target")
    .WithHttpEndpoint(port: 5001, name: "target-http")
    .WithHttpsEndpoint(port: 5002, name: "target-https");

builder.Build().Run();
