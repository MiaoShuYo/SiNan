using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using SiNan.Server.Contracts.Registry;
using SiNan.Server.Data;
using SiNan.Server.Data.Entities;
using SiNan.Server.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var provider = builder.Configuration.GetValue<string>("Data:Provider") ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("SiNan") ?? "Data Source=sinan.db";

builder.Services.AddDbContext<SiNan.Server.Data.SiNanDbContext>(options =>
{
    if (provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
    {
        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)));
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

builder.Services.AddScoped<SiNan.Server.Storage.IServiceRegistryRepository, SiNan.Server.Storage.EfServiceRegistryRepository>();
builder.Services.AddScoped<SiNan.Server.Storage.IConfigRepository, SiNan.Server.Storage.EfConfigRepository>();
builder.Services.AddScoped<SiNan.Server.Storage.IAuditLogRepository, SiNan.Server.Storage.EfAuditLogRepository>();
builder.Services.AddScoped<SiNan.Server.Storage.IUnitOfWork, SiNan.Server.Storage.EfUnitOfWork>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

var registryGroup = app.MapGroup("/api/v1/registry");

registryGroup.MapPost("/register", async (
    RegisterInstanceRequest request,
    IServiceRegistryRepository registry,
    IUnitOfWork unitOfWork,
    CancellationToken cancellationToken) =>
{
    var errors = RegistryRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.BadRequest(new { errors });
    }

    var now = DateTimeOffset.UtcNow;
    var service = await registry.GetServiceAsync(request.Namespace, request.Group, request.ServiceName, cancellationToken);

    if (service is null)
    {
        service = new ServiceEntity
        {
            Id = Guid.NewGuid(),
            Namespace = request.Namespace,
            Group = request.Group,
            Name = request.ServiceName,
            MetadataJson = "{}",
            Revision = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        await registry.AddServiceAsync(service, cancellationToken);
    }

    var instance = await registry.GetInstanceAsync(service.Id, request.Host, request.Port, cancellationToken);
    if (instance is null)
    {
        instance = new ServiceInstanceEntity
        {
            Id = Guid.NewGuid(),
            ServiceId = service.Id,
            InstanceId = $"{request.Host}:{request.Port}",
            Host = request.Host,
            Port = request.Port,
            Weight = request.Weight,
            Healthy = true,
            MetadataJson = MetadataJson.Serialize(request.Metadata),
            LastHeartbeatAt = now,
            TtlSeconds = request.TtlSeconds,
            IsEphemeral = request.IsEphemeral,
            CreatedAt = now,
            UpdatedAt = now
        };

        await registry.AddInstanceAsync(instance, cancellationToken);
    }
    else
    {
        instance.Weight = request.Weight;
        instance.MetadataJson = MetadataJson.Serialize(request.Metadata);
        instance.LastHeartbeatAt = now;
        instance.TtlSeconds = request.TtlSeconds;
        instance.IsEphemeral = request.IsEphemeral;
        instance.Healthy = true;
        instance.UpdatedAt = now;

        await registry.UpdateInstanceAsync(instance, cancellationToken);
    }

    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Results.Ok(new { instanceId = instance.InstanceId, serviceId = service.Id });
});

registryGroup.MapPost("/deregister", async (
    DeregisterInstanceRequest request,
    IServiceRegistryRepository registry,
    IUnitOfWork unitOfWork,
    CancellationToken cancellationToken) =>
{
    var errors = RegistryRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.BadRequest(new { errors });
    }

    var service = await registry.GetServiceAsync(request.Namespace, request.Group, request.ServiceName, cancellationToken);
    if (service is null)
    {
        return Results.NotFound(new { error = "Service not found." });
    }

    var instance = await registry.GetInstanceAsync(service.Id, request.Host, request.Port, cancellationToken);
    if (instance is null)
    {
        return Results.NotFound(new { error = "Instance not found." });
    }

    await registry.DeleteInstanceAsync(instance, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Results.Ok();
});

registryGroup.MapPost("/heartbeat", async (
    HeartbeatRequest request,
    IServiceRegistryRepository registry,
    IUnitOfWork unitOfWork,
    CancellationToken cancellationToken) =>
{
    var errors = RegistryRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.BadRequest(new { errors });
    }

    var service = await registry.GetServiceAsync(request.Namespace, request.Group, request.ServiceName, cancellationToken);
    if (service is null)
    {
        return Results.NotFound(new { error = "Service not found." });
    }

    var instance = await registry.GetInstanceAsync(service.Id, request.Host, request.Port, cancellationToken);
    if (instance is null)
    {
        return Results.NotFound(new { error = "Instance not found." });
    }

    instance.LastHeartbeatAt = DateTimeOffset.UtcNow;
    instance.Healthy = true;
    instance.UpdatedAt = DateTimeOffset.UtcNow;

    await registry.UpdateInstanceAsync(instance, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Results.Ok(new { instanceId = instance.InstanceId });
});

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
