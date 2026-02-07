/// <summary>
/// SiNan distributed application orchestration entry point
/// Uses .NET Aspire to orchestrate server and console applications
/// </summary>

// Create distributed application builder
var builder = DistributedApplication.CreateBuilder(args);

// Add SiNan server project (uses port 5043 from launchSettings.json)
var server = builder.AddProject<Projects.SiNan_Server>("sinan-server");

// Add SiNan console project with reference to server (uses port 5044)
builder.AddProject<Projects.SiNan_Console>("sinan-console")
    .WithReference(server);

// Build and run the application
builder.Build().Run();
