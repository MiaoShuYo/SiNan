/// <summary>
/// Service registry controller
/// Handles service instance registration, deregistration, heartbeat, query, and subscription operations
/// </summary>

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SiNan.Server.Audit;
using SiNan.Server.Auth;
using SiNan.Server.Contracts.Common;
using SiNan.Server.Contracts.Registry;
using SiNan.Server.Data;
using SiNan.Server.Data.Entities;
using SiNan.Server.Helpers;
using SiNan.Server.Quotas;
using SiNan.Server.Registry;
using SiNan.Server.Storage;

namespace SiNan.Server.Controllers;

[ApiController]
[Route("api/v1/registry")]
public class RegistryController : ControllerBase
{
    private readonly IServiceRegistryRepository _registry;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RegistryChangeNotifier _notifier;
    private readonly ApiKeyAuthorizationService _authService;
    private readonly AuditLogWriter _auditLogWriter;
    private readonly SiNanDbContext _dbContext;
    private readonly IOptions<QuotaOptions> _quotaOptions;

    public RegistryController(
        IServiceRegistryRepository registry,
        IUnitOfWork unitOfWork,
        RegistryChangeNotifier notifier,
        ApiKeyAuthorizationService authService,
        AuditLogWriter auditLogWriter,
        SiNanDbContext dbContext,
        IOptions<QuotaOptions> quotaOptions)
    {
        _registry = registry;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
        _authService = authService;
        _auditLogWriter = auditLogWriter;
        _dbContext = dbContext;
        _quotaOptions = quotaOptions;
    }

    /// <summary>
    /// Register a service instance
    /// Creates a new service if it doesn't exist, then adds or updates the instance
    /// </summary>
    /// <param name="request">Registration request containing service and instance details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Registration response with instance ID</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterInstanceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterInstanceRequest request,
        CancellationToken cancellationToken)
    {
        // Validate request parameters
        var errors = RegistryRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Invalid register request.", StatusCodes.Status400BadRequest, errors);
        }

        // Check authorization
        var registerResource = $"registry:{request.Namespace}/{request.Group}/{request.ServiceName}/{request.Host}:{request.Port}";
        var authResult = _authService.AuthorizeAction(HttpContext, request.Namespace, request.Group, "registry.register", registerResource);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var now = DateTimeOffset.UtcNow;
        var service = await _registry.GetServiceAsync(request.Namespace, request.Group, request.ServiceName, cancellationToken);

        var serviceExists = service is not null;

        if (!serviceExists)
        {
            var maxServices = _quotaOptions.Value.MaxServicesPerNamespace;
            if (maxServices > 0)
            {
                var serviceCount = await _dbContext.Services
                    .AsNoTracking()
                    .CountAsync(s => s.Namespace == request.Namespace, cancellationToken);
                if (serviceCount >= maxServices)
                {
                    return ErrorHelper.CreateError(HttpContext, ErrorCodes.QuotaExceeded, "Service quota exceeded.", StatusCodes.Status403Forbidden);
                }
            }

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

            await _registry.AddServiceAsync(service, cancellationToken);
        }
        else
        {
            service!.UpdatedAt = now;
            await _registry.UpdateServiceAsync(service, cancellationToken);
        }

        var instance = await _registry.GetInstanceAsync(service!.Id, request.Host, request.Port, cancellationToken);
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
            var maxInstances = _quotaOptions.Value.MaxInstancesPerNamespace;
            if (maxInstances > 0)
            {
                var instanceCount = await _dbContext.ServiceInstances
                    .AsNoTracking()
                    .CountAsync(i => i.Service != null && i.Service.Namespace == request.Namespace, cancellationToken);
                if (instanceCount >= maxInstances)
                {
                    return ErrorHelper.CreateError(HttpContext, ErrorCodes.QuotaExceeded, "Instance quota exceeded.", StatusCodes.Status403Forbidden);
                }
            }

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

            await _registry.AddInstanceAsync(instance, cancellationToken);
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

            await _registry.UpdateInstanceAsync(instance, cancellationToken);
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
        await _auditLogWriter.AddAsync(actor, auditAction, auditResource, beforeInstance, afterInstance, HttpContext.TraceIdentifier, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _notifier.Notify(RegistryHelper.BuildServiceKey(request.Namespace, request.Group, request.ServiceName));

        return Ok(new RegisterInstanceResponse { InstanceId = instance.InstanceId, ServiceId = service.Id });
    }

    [HttpPost("deregister")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deregister(
        [FromBody] DeregisterInstanceRequest request,
        CancellationToken cancellationToken)
    {
        var errors = RegistryRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Invalid deregister request.", StatusCodes.Status400BadRequest, errors);
        }

        var deregisterResource = $"registry:{request.Namespace}/{request.Group}/{request.ServiceName}/{request.Host}:{request.Port}";
        var authResult = _authService.AuthorizeAction(HttpContext, request.Namespace, request.Group, "registry.deregister", deregisterResource);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var service = await _registry.GetServiceAsync(request.Namespace, request.Group, request.ServiceName, cancellationToken);
        if (service is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ServiceNotFound, "Service not found.", StatusCodes.Status404NotFound);
        }

        var instance = await _registry.GetInstanceAsync(service.Id, request.Host, request.Port, cancellationToken);
        if (instance is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.InstanceNotFound, "Instance not found.", StatusCodes.Status404NotFound);
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

        await _registry.DeleteInstanceAsync(instance, cancellationToken);
        service.UpdatedAt = DateTimeOffset.UtcNow;
        await _registry.UpdateServiceAsync(service, cancellationToken);

        var actor = authResult.Actor;
        var auditResource = $"registry:{request.Namespace}/{request.Group}/{request.ServiceName}/{instance.InstanceId}";
        await _auditLogWriter.AddAsync(actor, "registry.deregister", auditResource, beforeInstance, null, HttpContext.TraceIdentifier, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _notifier.Notify(RegistryHelper.BuildServiceKey(request.Namespace, request.Group, request.ServiceName));

        return Ok();
    }

    [HttpPost("heartbeat")]
    [ProducesResponseType(typeof(HeartbeatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Heartbeat(
        [FromBody] HeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        var errors = RegistryRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Invalid heartbeat request.", StatusCodes.Status400BadRequest, errors);
        }

        var heartbeatResource = $"registry:{request.Namespace}/{request.Group}/{request.ServiceName}/{request.Host}:{request.Port}";
        var authResult = _authService.AuthorizeAction(HttpContext, request.Namespace, request.Group, "registry.heartbeat", heartbeatResource);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var service = await _registry.GetServiceAsync(request.Namespace, request.Group, request.ServiceName, cancellationToken);
        if (service is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ServiceNotFound, "Service not found.", StatusCodes.Status404NotFound);
        }

        var instance = await _registry.GetInstanceAsync(service.Id, request.Host, request.Port, cancellationToken);
        if (instance is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.InstanceNotFound, "Instance not found.", StatusCodes.Status404NotFound);
        }

        instance.LastHeartbeatAt = DateTimeOffset.UtcNow;
        instance.Healthy = true;
        instance.UpdatedAt = DateTimeOffset.UtcNow;

        await _registry.UpdateInstanceAsync(instance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Ok(new HeartbeatResponse { InstanceId = instance.InstanceId });
    }

    [HttpGet("instances")]
    [ProducesResponseType(typeof(ServiceInstancesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInstances(
        [FromQuery] string @namespace,
        [FromQuery] string group,
        [FromQuery] string serviceName,
        [FromQuery] bool? healthyOnly,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(@namespace) || string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(serviceName))
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Namespace, group, and serviceName are required.", StatusCodes.Status400BadRequest);
        }

        var readResource = $"registry:{@namespace}/{group}/{serviceName}";
        var authResult = _authService.AuthorizeAction(HttpContext, @namespace, group, "registry.read", readResource);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var service = await _registry.GetServiceAsync(@namespace, group, serviceName, cancellationToken);
        if (service is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ServiceNotFound, "Service not found.", StatusCodes.Status404NotFound);
        }

        var instances = await _registry.ListInstancesAsync(service.Id, healthyOnly ?? true, cancellationToken);
        var etagValue = RegistryHelper.BuildEtag(service, instances);

        if (Request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch) && ifNoneMatch == etagValue)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        var response = RegistryHelper.BuildInstancesResponse(@namespace, group, serviceName, instances, etagValue);

        Response.Headers.ETag = etagValue;
        return Ok(response);
    }

    [HttpGet("subscribe")]
    [ProducesResponseType(typeof(ServiceInstancesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Subscribe(
        [FromQuery] string @namespace,
        [FromQuery] string group,
        [FromQuery] string serviceName,
        [FromQuery] bool? healthyOnly,
        [FromQuery] int? timeoutMs,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(@namespace) || string.IsNullOrWhiteSpace(group) || string.IsNullOrWhiteSpace(serviceName))
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Namespace, group, and serviceName are required.", StatusCodes.Status400BadRequest);
        }

        var readResource = $"registry:{@namespace}/{group}/{serviceName}";
        var authResult = _authService.AuthorizeAction(HttpContext, @namespace, group, "registry.read", readResource);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var service = await _registry.GetServiceAsync(@namespace, group, serviceName, cancellationToken);
        if (service is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ServiceNotFound, "Service not found.", StatusCodes.Status404NotFound);
        }

        var instances = await _registry.ListInstancesAsync(service.Id, healthyOnly ?? true, cancellationToken);
        var etagValue = RegistryHelper.BuildEtag(service, instances);

        if (!Request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch) || ifNoneMatch != etagValue)
        {
            Response.Headers.ETag = etagValue;
            return Ok(RegistryHelper.BuildInstancesResponse(@namespace, group, serviceName, instances, etagValue));
        }

        var timeout = TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs ?? 30000, 1000, 60000));
        var key = RegistryHelper.BuildServiceKey(@namespace, group, serviceName);
        var currentVersion = _notifier.GetVersion(key);
        var changed = await _notifier.WaitForChangeAsync(key, currentVersion, timeout, cancellationToken);

        if (!changed)
        {
            Response.Headers.ETag = etagValue;
            return StatusCode(StatusCodes.Status304NotModified);
        }

        service = await _registry.GetServiceAsync(@namespace, group, serviceName, cancellationToken);
        if (service is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ServiceNotFound, "Service not found.", StatusCodes.Status404NotFound);
        }

        instances = await _registry.ListInstancesAsync(service.Id, healthyOnly ?? true, cancellationToken);
        etagValue = RegistryHelper.BuildEtag(service, instances);
        Response.Headers.ETag = etagValue;
        return Ok(RegistryHelper.BuildInstancesResponse(@namespace, group, serviceName, instances, etagValue));
    }

    [HttpGet("services")]
    [ProducesResponseType(typeof(List<ServiceSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServices(
        [FromQuery] string? @namespace,
        [FromQuery] string? group,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Services.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(@namespace))
        {
            query = query.Where(service => service.Namespace == @namespace);
        }

        if (!string.IsNullOrWhiteSpace(group))
        {
            query = query.Where(service => service.Group == group);
        }

        var sampleNamespace = string.IsNullOrWhiteSpace(@namespace) ? "default" : @namespace;
        var sampleGroup = string.IsNullOrWhiteSpace(group) ? "DEFAULT_GROUP" : group;
        var authResult = _authService.AuthorizeAction(HttpContext, sampleNamespace, sampleGroup, "registry.read", "registry:list");
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
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

        return Ok(items);
    }
}
