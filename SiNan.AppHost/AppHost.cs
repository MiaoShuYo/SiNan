/// <summary>
/// SiNan distributed application orchestration entry point
/// Uses .NET Aspire to orchestrate server and console applications
/// </summary>

// Create distributed application builder
var builder = DistributedApplication.CreateBuilder(args);

// Add SiNan server project with explicit HTTP endpoint on port 5043
var server = builder.AddProject<Projects.SiNan_Server>("sinan-server")
    .WithHttpEndpoint(port: 5043, name: "http");

// Add SiNan console project with reference to server
builder.AddProject<Projects.SiNan_Console>("sinan-console")
    .WithReference(server)
    .WithHttpEndpoint(port: 5044, name: "http");

// Build and run the application
builder.Build().Run();
