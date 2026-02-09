using Projects;

var builder = DistributedApplication.CreateBuilder(args);

builder.AddDockerComposeEnvironment("env");

var redis = builder.AddRedis("redis");

var timescaledb = builder.AddPostgres("timescaledb")
        .WithImage("timescale/timescaledb:latest-pg18");

var myiotdb = timescaledb.AddDatabase("myiotdb");

builder.AddProject<MyIOT_Api>("api")
    .WithReference(myiotdb)
    .WaitFor(timescaledb);

builder.Build().Run();
