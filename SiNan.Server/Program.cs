using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using SiNan.Server.Contracts.Registry;
using SiNan.Server.Data;
using SiNan.Server.Data.Entities;
using SiNan.Server.Registry;
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
builder.Services.AddSingleton<RegistryChangeNotifier>();
builder.Services.Configure<RegistryHealthOptions>(builder.Configuration.GetSection("Registry:Health"));
builder.Services.AddHostedService<RegistryHealthMonitor>();

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
    RegistryChangeNotifier notifier,
    CancellationToken cancellationToken) =>
{
    var errors = RegistryRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.BadRequest(new { errors });
    }

    var now = DateTimeOffset.UtcNow;
    var service = await registry.GetServiceAsync(request.Namespace, request.Group, request.ServiceName, cancellationToken);

    var serviceExists = service is not null;

    if (!serviceExists)
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
    else
    {
        service.UpdatedAt = now;
        await registry.UpdateServiceAsync(service, cancellationToken);
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

    notifier.Notify(BuildServiceKey(request.Namespace, request.Group, request.ServiceName));

    return Results.Ok(new { instanceId = instance.InstanceId, serviceId = service.Id });
});

registryGroup.MapPost("/deregister", async (
    DeregisterInstanceRequest request,
    IServiceRegistryRepository registry,
    IUnitOfWork unitOfWork,
    RegistryChangeNotifier notifier,
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
    service.UpdatedAt = DateTimeOffset.UtcNow;
    await registry.UpdateServiceAsync(service, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    notifier.Notify(BuildServiceKey(request.Namespace, request.Group, request.ServiceName));

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

registryGroup.MapGet("/instances", async (
    string @namespace,
    string group,
    string serviceName,
    bool? healthyOnly,
    HttpRequest httpRequest,
    HttpResponse httpResponse,
    IServiceRegistryRepository registry,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(@namespace) || string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(serviceName))
    {
        return Results.BadRequest(new { error = "Namespace, group, and serviceName are required." });
    }

    var service = await registry.GetServiceAsync(@namespace, group, serviceName, cancellationToken);
    if (service is null)
    {
        return Results.NotFound(new { error = "Service not found." });
    }

    var instances = await registry.ListInstancesAsync(service.Id, healthyOnly ?? true, cancellationToken);
    var etagValue = BuildEtag(service, instances);

    if (httpRequest.Headers.TryGetValue("If-None-Match", out var ifNoneMatch) && ifNoneMatch == etagValue)
    {
        return Results.StatusCode(StatusCodes.Status304NotModified);
    }

    var response = BuildInstancesResponse(@namespace, group, serviceName, instances, etagValue);

    httpResponse.Headers.ETag = etagValue;
    return Results.Ok(response);
});

registryGroup.MapGet("/subscribe", async (
    string @namespace,
    string group,
    string serviceName,
    bool? healthyOnly,
    int? timeoutMs,
    HttpRequest httpRequest,
    HttpResponse httpResponse,
    IServiceRegistryRepository registry,
    RegistryChangeNotifier notifier,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(@namespace) || string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(serviceName))
    {
        return Results.BadRequest(new { error = "Namespace, group, and serviceName are required." });
    }

    var service = await registry.GetServiceAsync(@namespace, group, serviceName, cancellationToken);
    if (service is null)
    {
        return Results.NotFound(new { error = "Service not found." });
    }

    var instances = await registry.ListInstancesAsync(service.Id, healthyOnly ?? true, cancellationToken);
    var etagValue = BuildEtag(service, instances);

    if (!httpRequest.Headers.TryGetValue("If-None-Match", out var ifNoneMatch) || ifNoneMatch != etagValue)
    {
        httpResponse.Headers.ETag = etagValue;
        return Results.Ok(BuildInstancesResponse(@namespace, group, serviceName, instances, etagValue));
    }

    var timeout = TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs ?? 30000, 1000, 60000));
    var key = BuildServiceKey(@namespace, group, serviceName);
    var currentVersion = notifier.GetVersion(key);
    var changed = await notifier.WaitForChangeAsync(key, currentVersion, timeout, cancellationToken);

    if (!changed)
    {
        httpResponse.Headers.ETag = etagValue;
        return Results.StatusCode(StatusCodes.Status304NotModified);
    }

    service = await registry.GetServiceAsync(@namespace, group, serviceName, cancellationToken);
    if (service is null)
    {
        return Results.NotFound(new { error = "Service not found." });
    }

    instances = await registry.ListInstancesAsync(service.Id, healthyOnly ?? true, cancellationToken);
    etagValue = BuildEtag(service, instances);
    httpResponse.Headers.ETag = etagValue;
    return Results.Ok(BuildInstancesResponse(@namespace, group, serviceName, instances, etagValue));
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

static string BuildServiceKey(string @namespace, string group, string serviceName)
{
    return $"{@namespace}::{group}::{serviceName}";
}

static string BuildEtag(ServiceEntity service, IReadOnlyList<ServiceInstanceEntity> instances)
{
    var latestUpdate = instances.Count == 0
        ? service.UpdatedAt
        : instances.Max(i => i.UpdatedAt);
    return $"\"{service.Id}:{latestUpdate.ToUnixTimeMilliseconds()}:{instances.Count}\"";
}

static ServiceInstancesResponse BuildInstancesResponse(
    string @namespace,
    string group,
    string serviceName,
    IReadOnlyList<ServiceInstanceEntity> instances,
    string etagValue)
{
    return new ServiceInstancesResponse
    {
        Namespace = @namespace,
        Group = group,
        ServiceName = serviceName,
        ETag = etagValue,
        Instances = instances.Select(instance => new ServiceInstanceDto
        {
            InstanceId = instance.InstanceId,
            Host = instance.Host,
            Port = instance.Port,
            Weight = instance.Weight,
            Healthy = instance.Healthy,
            TtlSeconds = instance.TtlSeconds,
            IsEphemeral = instance.IsEphemeral,
            Metadata = MetadataParser.Parse(instance.MetadataJson)
        }).ToList()
    };
}

public partial class Program
{
}
