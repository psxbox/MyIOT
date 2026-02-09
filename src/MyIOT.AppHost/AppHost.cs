using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("env");

var redis = builder.AddRedis("redis");

var timescaledb = builder.AddPostgres("timescaledb")
    .WithImage("timescale/timescaledb:latest-pg16")
    .WithDataVolume("pgdata")
    .WithEnvironment("POSTGRES_USER", "postgres")
    .WithEnvironment("POSTGRES_PASSWORD", "postgres")
    .WithEnvironment("POSTGRES_DB", "myiot");

builder.AddProject<MyIOT_Api>("api")
    .WithHttpHealthCheck()
    .WaitFor(timescaledb)
    .WaitFor(redis);

builder.Build().Run();
