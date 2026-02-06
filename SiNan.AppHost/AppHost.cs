var builder = DistributedApplication.CreateBuilder(args);

var server = builder.AddProject<Projects.SiNan_Server>("sinan-server");
builder.AddProject<Projects.SiNan_Console>("sinan-console")
    .WithReference(server);

builder.Build().Run();
