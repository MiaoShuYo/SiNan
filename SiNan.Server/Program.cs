using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using SiNan.Server.Audit;
using SiNan.Server.Auth;
using SiNan.Server.Config;
using SiNan.Server.Contracts.Common;
using SiNan.Server.Contracts.Config;
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
builder.Services.AddScoped<AuditLogWriter>();
builder.Services.AddSingleton<RegistryChangeNotifier>();
builder.Services.AddSingleton<ConfigChangeNotifier>();
builder.Services.Configure<RegistryHealthOptions>(builder.Configuration.GetSection("Registry:Health"));
builder.Services.AddHostedService<RegistryHealthMonitor>();
builder.Services.Configure<ConfigHistoryCleanupOptions>(builder.Configuration.GetSection("Config:HistoryCleanup"));
builder.Services.AddHostedService<ConfigHistoryCleanupService>();
builder.Services.Configure<ApiKeyAuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.AddSingleton<ApiKeyAuthorizationService>();

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
var configGroup = app.MapGroup("/api/v1/configs");

registryGroup.MapPost("/register", async (
    RegisterInstanceRequest request,
    IServiceRegistryRepository registry,
    IUnitOfWork unitOfWork,
    RegistryChangeNotifier notifier,
    ApiKeyAuthorizationService authService,
    AuditLogWriter auditLogWriter,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var errors = RegistryRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Error(httpContext, ErrorCodes.ValidationFailed, "Invalid register request.", StatusCodes.Status400BadRequest, errors);
    }

    var authResult = authService.AuthorizeNamespaceGroup(httpContext, request.Namespace, request.Group);
    if (!authResult.Allowed)
    {
        return Error(httpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
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
    var isNewInstance = instance is null;
    var beforeInstance = instance is null
        ? null
        : new
        {
            instance.InstanceId,
            instance.Host,
            instance.Port,
            instance.Weight,
            instance.Healthy,
            instance.TtlSeconds,
            instance.IsEphemeral,
            instance.MetadataJson
        };

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

    var afterInstance = new
    {
        instance.InstanceId,
        instance.Host,
        instance.Port,
        instance.Weight,
        instance.Healthy,
        instance.TtlSeconds,
        instance.IsEphemeral,
        instance.MetadataJson
    };

    var actor = authResult.Actor;
    var auditAction = isNewInstance ? "registry.register" : "registry.update";
    var auditResource = $"registry:{request.Namespace}/{request.Group}/{request.ServiceName}/{instance.InstanceId}";
    await auditLogWriter.AddAsync(actor, auditAction, auditResource, beforeInstance, afterInstance, httpContext.TraceIdentifier, cancellationToken);

    await unitOfWork.SaveChangesAsync(cancellationToken);

    notifier.Notify(BuildServiceKey(request.Namespace, request.Group, request.ServiceName));

    return Results.Ok(new RegisterInstanceResponse { InstanceId = instance.InstanceId, ServiceId = service.Id });
})
    .WithName("RegistryRegister")
    .Produces<RegisterInstanceResponse>(StatusCodes.Status200OK)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .WithOpenApi();

registryGroup.MapPost("/deregister", async (
    DeregisterInstanceRequest request,
    IServiceRegistryRepository registry,
    IUnitOfWork unitOfWork,
    RegistryChangeNotifier notifier,
    ApiKeyAuthorizationService authService,
    AuditLogWriter auditLogWriter,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var errors = RegistryRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Error(httpContext, ErrorCodes.ValidationFailed, "Invalid deregister request.", StatusCodes.Status400BadRequest, errors);
    }

    var authResult = authService.AuthorizeNamespaceGroup(httpContext, request.Namespace, request.Group);
    if (!authResult.Allowed)
    {
        return Error(httpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
    }

    var service = await registry.GetServiceAsync(request.Namespace, request.Group, request.ServiceName, cancellationToken);
    if (service is null)
    {
        return Error(httpContext, ErrorCodes.ServiceNotFound, "Service not found.", StatusCodes.Status404NotFound);
    }

    var instance = await registry.GetInstanceAsync(service.Id, request.Host, request.Port, cancellationToken);
    if (instance is null)
    {
        return Error(httpContext, ErrorCodes.InstanceNotFound, "Instance not found.", StatusCodes.Status404NotFound);
    }

    var beforeInstance = new
    {
        instance.InstanceId,
        instance.Host,
        instance.Port,
        instance.Weight,
        instance.Healthy,
        instance.TtlSeconds,
        instance.IsEphemeral,
        instance.MetadataJson
    };

    await registry.DeleteInstanceAsync(instance, cancellationToken);
    service.UpdatedAt = DateTimeOffset.UtcNow;
    await registry.UpdateServiceAsync(service, cancellationToken);

    var actor = authResult.Actor;
    var auditResource = $"registry:{request.Namespace}/{request.Group}/{request.ServiceName}/{instance.InstanceId}";
    await auditLogWriter.AddAsync(actor, "registry.deregister", auditResource, beforeInstance, null, httpContext.TraceIdentifier, cancellationToken);

    await unitOfWork.SaveChangesAsync(cancellationToken);

    notifier.Notify(BuildServiceKey(request.Namespace, request.Group, request.ServiceName));

    return Results.Ok();
})
    .WithName("RegistryDeregister")
    .Produces(StatusCodes.Status200OK)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .WithOpenApi();

registryGroup.MapPost("/heartbeat", async (
    HeartbeatRequest request,
    IServiceRegistryRepository registry,
    IUnitOfWork unitOfWork,
    ApiKeyAuthorizationService authService,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var errors = RegistryRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Error(httpContext, ErrorCodes.ValidationFailed, "Invalid heartbeat request.", StatusCodes.Status400BadRequest, errors);
    }

    var authResult = authService.AuthorizeNamespaceGroup(httpContext, request.Namespace, request.Group);
    if (!authResult.Allowed)
    {
        return Error(httpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
    }

    var service = await registry.GetServiceAsync(request.Namespace, request.Group, request.ServiceName, cancellationToken);
    if (service is null)
    {
        return Error(httpContext, ErrorCodes.ServiceNotFound, "Service not found.", StatusCodes.Status404NotFound);
    }

    var instance = await registry.GetInstanceAsync(service.Id, request.Host, request.Port, cancellationToken);
    if (instance is null)
    {
        return Error(httpContext, ErrorCodes.InstanceNotFound, "Instance not found.", StatusCodes.Status404NotFound);
    }

    instance.LastHeartbeatAt = DateTimeOffset.UtcNow;
    instance.Healthy = true;
    instance.UpdatedAt = DateTimeOffset.UtcNow;

    await registry.UpdateInstanceAsync(instance, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    return Results.Ok(new HeartbeatResponse { InstanceId = instance.InstanceId });
})
    .WithName("RegistryHeartbeat")
    .Produces<HeartbeatResponse>(StatusCodes.Status200OK)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .WithOpenApi();

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
        return Error(httpRequest.HttpContext, ErrorCodes.ValidationFailed, "Namespace, group, and serviceName are required.", StatusCodes.Status400BadRequest);
    }

    var service = await registry.GetServiceAsync(@namespace, group, serviceName, cancellationToken);
    if (service is null)
    {
        return Error(httpRequest.HttpContext, ErrorCodes.ServiceNotFound, "Service not found.", StatusCodes.Status404NotFound);
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
})
    .WithName("RegistryInstances")
    .Produces<ServiceInstancesResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status304NotModified)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .WithOpenApi();

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
        return Error(httpRequest.HttpContext, ErrorCodes.ValidationFailed, "Namespace, group, and serviceName are required.", StatusCodes.Status400BadRequest);
    }

    var service = await registry.GetServiceAsync(@namespace, group, serviceName, cancellationToken);
    if (service is null)
    {
        return Error(httpRequest.HttpContext, ErrorCodes.ServiceNotFound, "Service not found.", StatusCodes.Status404NotFound);
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
        return Error(httpRequest.HttpContext, ErrorCodes.ServiceNotFound, "Service not found.", StatusCodes.Status404NotFound);
    }

    instances = await registry.ListInstancesAsync(service.Id, healthyOnly ?? true, cancellationToken);
    etagValue = BuildEtag(service, instances);
    httpResponse.Headers.ETag = etagValue;
    return Results.Ok(BuildInstancesResponse(@namespace, group, serviceName, instances, etagValue));
})
    .WithName("RegistrySubscribe")
    .Produces<ServiceInstancesResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status304NotModified)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .WithOpenApi();

registryGroup.MapGet("/services", async (
    string? @namespace,
    string? group,
    SiNanDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var query = dbContext.Services.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(@namespace))
    {
        query = query.Where(service => service.Namespace == @namespace);
    }

    if (!string.IsNullOrWhiteSpace(group))
    {
        query = query.Where(service => service.Group == group);
    }

    var items = await query
        .Select(service => new ServiceSummaryResponse
        {
            Namespace = service.Namespace,
            Group = service.Group,
            ServiceName = service.Name,
            InstanceCount = service.Instances.Count,
            HealthyInstanceCount = service.Instances.Count(instance => instance.Healthy),
            UpdatedAt = service.UpdatedAt
        })
        .OrderBy(item => item.Namespace)
        .ThenBy(item => item.Group)
        .ThenBy(item => item.ServiceName)
        .ToListAsync(cancellationToken);

    return Results.Ok(items);
})
    .WithName("RegistryServices")
    .Produces<List<ServiceSummaryResponse>>(StatusCodes.Status200OK)
    .WithOpenApi();

configGroup.MapPost("/", async (
    ConfigUpsertRequest request,
    IConfigRepository configRepository,
    IUnitOfWork unitOfWork,
    ConfigChangeNotifier notifier,
    ApiKeyAuthorizationService authService,
    AuditLogWriter auditLogWriter,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var errors = ConfigRequestValidator.ValidateUpsert(request);
    if (errors.Count > 0)
    {
        return Error(httpContext, ErrorCodes.ValidationFailed, "Invalid config request.", StatusCodes.Status400BadRequest, errors);
    }

    var authResult = authService.AuthorizeNamespaceGroup(httpContext, request.Namespace, request.Group);
    if (!authResult.Allowed)
    {
        return Error(httpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
    }

    var existing = await configRepository.GetConfigAsync(request.Namespace, request.Group, request.Key, cancellationToken);
    if (existing is not null)
    {
        return Error(httpContext, ErrorCodes.ConfigAlreadyExists, "Config already exists.", StatusCodes.Status409Conflict);
    }

    var now = DateTimeOffset.UtcNow;
    var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "text/plain" : request.ContentType!;

    var config = new ConfigItemEntity
    {
        Id = Guid.NewGuid(),
        Namespace = request.Namespace,
        Group = request.Group,
        Key = request.Key,
        Content = request.Content,
        ContentType = contentType,
        Version = 1,
        PublishedAt = now,
        PublishedBy = request.PublishedBy,
        CreatedAt = now,
        UpdatedAt = now
    };

    await configRepository.AddConfigAsync(config, cancellationToken);
    await configRepository.AddHistoryAsync(new ConfigHistoryEntity
    {
        Id = Guid.NewGuid(),
        ConfigId = config.Id,
        Version = 1,
        Content = config.Content,
        ContentType = config.ContentType,
        PublishedAt = now,
        PublishedBy = config.PublishedBy,
        CreatedAt = now
    }, cancellationToken);

    await unitOfWork.SaveChangesAsync(cancellationToken);

    notifier.Notify(BuildConfigKey(config.Namespace, config.Group, config.Key));
    ConfigMetrics.RecordChange();

    var actor = authResult.Actor;
    var auditResource = $"config:{config.Namespace}/{config.Group}/{config.Key}";
    var afterConfig = new
    {
        config.Namespace,
        config.Group,
        config.Key,
        config.ContentType,
        config.Version
    };
    await auditLogWriter.AddAsync(actor, "config.create", auditResource, null, afterConfig, httpContext.TraceIdentifier, cancellationToken);

    return Results.Ok(ToConfigItemResponse(config));
})
    .WithName("ConfigCreate")
    .Produces<ConfigItemResponse>(StatusCodes.Status200OK)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
    .WithOpenApi();

configGroup.MapPut("/", async (
    ConfigUpsertRequest request,
    IConfigRepository configRepository,
    IUnitOfWork unitOfWork,
    ConfigChangeNotifier notifier,
    ApiKeyAuthorizationService authService,
    AuditLogWriter auditLogWriter,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var errors = ConfigRequestValidator.ValidateUpsert(request);
    if (errors.Count > 0)
    {
        return Error(httpContext, ErrorCodes.ValidationFailed, "Invalid config request.", StatusCodes.Status400BadRequest, errors);
    }

    var authResult = authService.AuthorizeNamespaceGroup(httpContext, request.Namespace, request.Group);
    if (!authResult.Allowed)
    {
        return Error(httpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
    }

    var existing = await configRepository.GetConfigAsync(request.Namespace, request.Group, request.Key, cancellationToken);
    if (existing is null)
    {
        return Error(httpContext, ErrorCodes.ConfigNotFound, "Config not found.", StatusCodes.Status404NotFound);
    }

    var beforeConfig = new
    {
        existing.Namespace,
        existing.Group,
        existing.Key,
        existing.ContentType,
        existing.Version
    };

    var now = DateTimeOffset.UtcNow;
    var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "text/plain" : request.ContentType!;
    var newVersion = existing.Version + 1;

    var updated = new ConfigItemEntity
    {
        Id = existing.Id,
        Namespace = existing.Namespace,
        Group = existing.Group,
        Key = existing.Key,
        Content = request.Content,
        ContentType = contentType,
        Version = newVersion,
        PublishedAt = now,
        PublishedBy = request.PublishedBy,
        CreatedAt = existing.CreatedAt,
        UpdatedAt = now
    };

    await configRepository.UpdateConfigAsync(updated, cancellationToken);
    await configRepository.AddHistoryAsync(new ConfigHistoryEntity
    {
        Id = Guid.NewGuid(),
        ConfigId = updated.Id,
        Version = newVersion,
        Content = updated.Content,
        ContentType = updated.ContentType,
        PublishedAt = now,
        PublishedBy = updated.PublishedBy,
        CreatedAt = now
    }, cancellationToken);

    await unitOfWork.SaveChangesAsync(cancellationToken);

    notifier.Notify(BuildConfigKey(updated.Namespace, updated.Group, updated.Key));
    ConfigMetrics.RecordChange();

    var actor = authResult.Actor;
    var auditResource = $"config:{updated.Namespace}/{updated.Group}/{updated.Key}";
    var afterConfig = new
    {
        updated.Namespace,
        updated.Group,
        updated.Key,
        updated.ContentType,
        updated.Version
    };
    await auditLogWriter.AddAsync(actor, "config.update", auditResource, beforeConfig, afterConfig, httpContext.TraceIdentifier, cancellationToken);

    return Results.Ok(ToConfigItemResponse(updated));
})
    .WithName("ConfigUpdate")
    .Produces<ConfigItemResponse>(StatusCodes.Status200OK)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .WithOpenApi();

configGroup.MapGet("/", async (
    string @namespace,
    string group,
    string key,
    IConfigRepository configRepository,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var errors = ConfigRequestValidator.ValidateKey(@namespace, group, key);
    if (errors.Count > 0)
    {
        return Error(httpContext, ErrorCodes.ValidationFailed, "Namespace, group, and key are required.", StatusCodes.Status400BadRequest, errors);
    }

    var config = await configRepository.GetConfigAsync(@namespace, group, key, cancellationToken);
    if (config is null)
    {
        return Error(httpContext, ErrorCodes.ConfigNotFound, "Config not found.", StatusCodes.Status404NotFound);
    }

    return Results.Ok(ToConfigItemResponse(config));
})
    .WithName("ConfigGet")
    .Produces<ConfigItemResponse>(StatusCodes.Status200OK)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .WithOpenApi();

configGroup.MapDelete("/", async (
    string @namespace,
    string group,
    string key,
    IConfigRepository configRepository,
    IUnitOfWork unitOfWork,
    ConfigChangeNotifier notifier,
    ApiKeyAuthorizationService authService,
    AuditLogWriter auditLogWriter,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var errors = ConfigRequestValidator.ValidateKey(@namespace, group, key);
    if (errors.Count > 0)
    {
        return Error(httpContext, ErrorCodes.ValidationFailed, "Namespace, group, and key are required.", StatusCodes.Status400BadRequest, errors);
    }

    var authResult = authService.AuthorizeNamespaceGroup(httpContext, @namespace, group);
    if (!authResult.Allowed)
    {
        return Error(httpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
    }

    var config = await configRepository.GetConfigAsync(@namespace, group, key, cancellationToken);
    if (config is null)
    {
        return Error(httpContext, ErrorCodes.ConfigNotFound, "Config not found.", StatusCodes.Status404NotFound);
    }

    var beforeConfig = new
    {
        config.Namespace,
        config.Group,
        config.Key,
        config.ContentType,
        config.Version
    };

    await configRepository.DeleteConfigAsync(config, cancellationToken);
    await unitOfWork.SaveChangesAsync(cancellationToken);

    notifier.Notify(BuildConfigKey(config.Namespace, config.Group, config.Key));
    ConfigMetrics.RecordChange();

    var actor = authResult.Actor;
    var auditResource = $"config:{config.Namespace}/{config.Group}/{config.Key}";
    await auditLogWriter.AddAsync(actor, "config.delete", auditResource, beforeConfig, null, httpContext.TraceIdentifier, cancellationToken);
    return Results.Ok();
})
    .WithName("ConfigDelete")
    .Produces(StatusCodes.Status200OK)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .WithOpenApi();

configGroup.MapGet("/history", async (
    string @namespace,
    string group,
    string key,
    IConfigRepository configRepository,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var errors = ConfigRequestValidator.ValidateKey(@namespace, group, key);
    if (errors.Count > 0)
    {
        return Error(httpContext, ErrorCodes.ValidationFailed, "Namespace, group, and key are required.", StatusCodes.Status400BadRequest, errors);
    }

    var config = await configRepository.GetConfigAsync(@namespace, group, key, cancellationToken);
    if (config is null)
    {
        return Error(httpContext, ErrorCodes.ConfigNotFound, "Config not found.", StatusCodes.Status404NotFound);
    }

    var history = await configRepository.GetHistoryAsync(config.Id, cancellationToken);
    var response = history.Select(item => new ConfigHistoryResponse
    {
        Version = item.Version,
        Content = item.Content,
        ContentType = item.ContentType,
        PublishedAt = item.PublishedAt,
        PublishedBy = item.PublishedBy
    }).ToList();

    return Results.Ok(response);
})
    .WithName("ConfigHistory")
    .Produces<List<ConfigHistoryResponse>>(StatusCodes.Status200OK)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .WithOpenApi();

configGroup.MapGet("/list", async (
    string? @namespace,
    string? group,
    SiNanDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    IQueryable<ConfigItemEntity> query = dbContext.ConfigItems.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(@namespace))
    {
        query = query.Where(item => item.Namespace == @namespace);
    }

    if (!string.IsNullOrWhiteSpace(group))
    {
        query = query.Where(item => item.Group == group);
    }

    var items = await query
        .OrderBy(item => item.Namespace)
        .ThenBy(item => item.Group)
        .ThenBy(item => item.Key)
        .Select(item => new ConfigListItemResponse
        {
            Namespace = item.Namespace,
            Group = item.Group,
            Key = item.Key,
            ContentType = item.ContentType,
            Version = item.Version,
            PublishedAt = item.PublishedAt,
            UpdatedAt = item.UpdatedAt
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(items);
})
    .WithName("ConfigList")
    .Produces<List<ConfigListItemResponse>>(StatusCodes.Status200OK)
    .WithOpenApi();

configGroup.MapGet("/subscribe", async (
    string @namespace,
    string group,
    string key,
    int? timeoutMs,
    HttpRequest httpRequest,
    HttpResponse httpResponse,
    IConfigRepository configRepository,
    ConfigChangeNotifier notifier,
    CancellationToken cancellationToken) =>
{
    ConfigMetrics.RecordSubscribeRequest();

    var errors = ConfigRequestValidator.ValidateKey(@namespace, group, key);
    if (errors.Count > 0)
    {
        return Error(httpRequest.HttpContext, ErrorCodes.ValidationFailed, "Namespace, group, and key are required.", StatusCodes.Status400BadRequest, errors);
    }

    var config = await configRepository.GetConfigAsync(@namespace, group, key, cancellationToken);
    if (config is null)
    {
        return Error(httpRequest.HttpContext, ErrorCodes.ConfigNotFound, "Config not found.", StatusCodes.Status404NotFound);
    }

    var etagValue = BuildConfigEtag(config);
    if (!httpRequest.Headers.TryGetValue("If-None-Match", out var ifNoneMatch) || ifNoneMatch != etagValue)
    {
        httpResponse.Headers.ETag = etagValue;
        return Results.Ok(ToConfigItemResponse(config));
    }

    var timeout = TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs ?? 30000, 1000, 60000));
    var keyValue = BuildConfigKey(@namespace, group, key);
    var currentVersion = notifier.GetVersion(keyValue);
    var changed = await notifier.WaitForChangeAsync(keyValue, currentVersion, timeout, cancellationToken);

    if (!changed)
    {
        ConfigMetrics.RecordSubscribeTimeout();
        httpResponse.Headers.ETag = etagValue;
        return Results.StatusCode(StatusCodes.Status304NotModified);
    }

    ConfigMetrics.RecordSubscribeChange();

    config = await configRepository.GetConfigAsync(@namespace, group, key, cancellationToken);
    if (config is null)
    {
        return Error(httpRequest.HttpContext, ErrorCodes.ConfigNotFound, "Config not found.", StatusCodes.Status404NotFound);
    }

    etagValue = BuildConfigEtag(config);
    httpResponse.Headers.ETag = etagValue;
    return Results.Ok(ToConfigItemResponse(config));
})
    .WithName("ConfigSubscribe")
    .Produces<ConfigItemResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status304NotModified)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .WithOpenApi();

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

static ConfigItemResponse ToConfigItemResponse(ConfigItemEntity config)
{
    return new ConfigItemResponse
    {
        Namespace = config.Namespace,
        Group = config.Group,
        Key = config.Key,
        Content = config.Content,
        ContentType = config.ContentType,
        Version = config.Version,
        PublishedAt = config.PublishedAt,
        PublishedBy = config.PublishedBy,
        UpdatedAt = config.UpdatedAt
    };
}

static string BuildConfigKey(string @namespace, string group, string key)
{
    return $"{@namespace}::{group}::{key}";
}

static string BuildConfigEtag(ConfigItemEntity config)
{
    var publishedAt = config.PublishedAt?.ToUnixTimeMilliseconds() ?? 0;
    return $"\"{config.Id}:{config.Version}:{publishedAt}\"";
}

static IResult Error(HttpContext context, string code, string message, int statusCode, object? details = null)
{
    return Results.Json(new ErrorResponse
    {
        Code = code,
        Message = message,
        Details = details,
        TraceId = context.TraceIdentifier
    }, statusCode: statusCode);
}

public partial class Program
{
}
