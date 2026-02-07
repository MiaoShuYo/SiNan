using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SiNan.Server.Audit;
using SiNan.Server.Auth;
using SiNan.Server.Config;
using SiNan.Server.Contracts.Common;
using SiNan.Server.Contracts.Config;
using SiNan.Server.Data;
using SiNan.Server.Data.Entities;
using SiNan.Server.Helpers;
using SiNan.Server.Quotas;
using SiNan.Server.Storage;

namespace SiNan.Server.Controllers;

[ApiController]
[Route("api/v1/configs")]
public class ConfigController : ControllerBase
{
    private readonly IConfigRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ConfigChangeNotifier _notifier;
    private readonly ApiKeyAuthorizationService _authService;
    private readonly AuditLogWriter _auditLogWriter;
    private readonly SiNanDbContext _dbContext;
    private readonly IOptions<QuotaOptions> _quotaOptions;

    public ConfigController(
        IConfigRepository configRepository,
        IUnitOfWork unitOfWork,
        ConfigChangeNotifier notifier,
        ApiKeyAuthorizationService authService,
        AuditLogWriter auditLogWriter,
        SiNanDbContext dbContext,
        IOptions<QuotaOptions> quotaOptions)
    {
        _configRepository = configRepository;
        _unitOfWork = unitOfWork;
        _notifier = notifier;
        _authService = authService;
        _auditLogWriter = auditLogWriter;
        _dbContext = dbContext;
        _quotaOptions = quotaOptions;
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConfigItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] ConfigUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var errors = ConfigRequestValidator.ValidateUpsert(request);
        if (errors.Count > 0)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Invalid config request.", StatusCodes.Status400BadRequest, errors);
        }

        var configResource = $"config:{request.Namespace}/{request.Group}/{request.Key}";
        var authResult = _authService.AuthorizeAction(HttpContext, request.Namespace, request.Group, "config.create", configResource);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var existing = await _configRepository.GetConfigAsync(request.Namespace, request.Group, request.Key, cancellationToken);
        if (existing is not null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ConfigAlreadyExists, "Config already exists.", StatusCodes.Status409Conflict);
        }

        if (!ConfigHelper.ValidateConfigContentQuota(_quotaOptions.Value, request.Content, out var quotaError))
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.QuotaExceeded, quotaError, StatusCodes.Status403Forbidden);
        }

        var maxConfigs = _quotaOptions.Value.MaxConfigsPerNamespace;
        if (maxConfigs > 0)
        {
            var configCount = await _dbContext.ConfigItems
                .AsNoTracking()
                .CountAsync(c => c.Namespace == request.Namespace, cancellationToken);
            if (configCount >= maxConfigs)
            {
                return ErrorHelper.CreateError(HttpContext, ErrorCodes.QuotaExceeded, "Config quota exceeded.", StatusCodes.Status403Forbidden);
            }
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

        await _configRepository.AddConfigAsync(config, cancellationToken);
        await _configRepository.AddHistoryAsync(new ConfigHistoryEntity
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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _notifier.Notify(ConfigHelper.BuildConfigKey(config.Namespace, config.Group, config.Key));
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
        await _auditLogWriter.AddAsync(actor, "config.create", auditResource, null, afterConfig, HttpContext.TraceIdentifier, cancellationToken);

        return Ok(ConfigHelper.ToConfigItemResponse(config));
    }

    [HttpPut]
    [ProducesResponseType(typeof(ConfigItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromBody] ConfigUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var errors = ConfigRequestValidator.ValidateUpsert(request);
        if (errors.Count > 0)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Invalid config request.", StatusCodes.Status400BadRequest, errors);
        }

        var configResource = $"config:{request.Namespace}/{request.Group}/{request.Key}";
        var authResult = _authService.AuthorizeAction(HttpContext, request.Namespace, request.Group, "config.update", configResource);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var existing = await _configRepository.GetConfigAsync(request.Namespace, request.Group, request.Key, cancellationToken);
        if (existing is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ConfigNotFound, "Config not found.", StatusCodes.Status404NotFound);
        }

        if (!ConfigHelper.ValidateConfigContentQuota(_quotaOptions.Value, request.Content, out var quotaError))
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.QuotaExceeded, quotaError, StatusCodes.Status403Forbidden);
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

        await _configRepository.UpdateConfigAsync(updated, cancellationToken);
        await _configRepository.AddHistoryAsync(new ConfigHistoryEntity
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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _notifier.Notify(ConfigHelper.BuildConfigKey(updated.Namespace, updated.Group, updated.Key));
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
        await _auditLogWriter.AddAsync(actor, "config.update", auditResource, beforeConfig, afterConfig, HttpContext.TraceIdentifier, cancellationToken);

        return Ok(ConfigHelper.ToConfigItemResponse(updated));
    }

    [HttpGet]
    [ProducesResponseType(typeof(ConfigItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromQuery] string @namespace,
        [FromQuery] string group,
        [FromQuery] string key,
        CancellationToken cancellationToken)
    {
        var errors = ConfigRequestValidator.ValidateKey(@namespace, group, key);
        if (errors.Count > 0)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Namespace, group, and key are required.", StatusCodes.Status400BadRequest, errors);
        }

        var configResource = $"config:{@namespace}/{group}/{key}";
        var authResult = _authService.AuthorizeAction(HttpContext, @namespace, group, "config.read", configResource);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var config = await _configRepository.GetConfigAsync(@namespace, group, key, cancellationToken);
        if (config is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ConfigNotFound, "Config not found.", StatusCodes.Status404NotFound);
        }

        return Ok(ConfigHelper.ToConfigItemResponse(config));
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromQuery] string @namespace,
        [FromQuery] string group,
        [FromQuery] string key,
        CancellationToken cancellationToken)
    {
        var errors = ConfigRequestValidator.ValidateKey(@namespace, group, key);
        if (errors.Count > 0)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Namespace, group, and key are required.", StatusCodes.Status400BadRequest, errors);
        }

        var configResource = $"config:{@namespace}/{group}/{key}";
        var authResult = _authService.AuthorizeAction(HttpContext, @namespace, group, "config.delete", configResource);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var config = await _configRepository.GetConfigAsync(@namespace, group, key, cancellationToken);
        if (config is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ConfigNotFound, "Config not found.", StatusCodes.Status404NotFound);
        }

        var beforeConfig = new
        {
            config.Namespace,
            config.Group,
            config.Key,
            config.ContentType,
            config.Version
        };

        var history = await _configRepository.GetHistoryAsync(config.Id, cancellationToken);
        foreach (var item in history)
        {
            await _configRepository.DeleteHistoryAsync(item, cancellationToken);
        }

        await _configRepository.DeleteConfigAsync(config, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _notifier.Notify(ConfigHelper.BuildConfigKey(config.Namespace, config.Group, config.Key));
        ConfigMetrics.RecordChange();

        var actor = authResult.Actor;
        var auditResource = $"config:{config.Namespace}/{config.Group}/{config.Key}";
        await _auditLogWriter.AddAsync(actor, "config.delete", auditResource, beforeConfig, null, HttpContext.TraceIdentifier, cancellationToken);
        
        return Ok();
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(List<ConfigHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string @namespace,
        [FromQuery] string group,
        [FromQuery] string key,
        CancellationToken cancellationToken)
    {
        var errors = ConfigRequestValidator.ValidateKey(@namespace, group, key);
        if (errors.Count > 0)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Namespace, group, and key are required.", StatusCodes.Status400BadRequest, errors);
        }

        var configResource = $"config:{@namespace}/{group}/{key}";
        var authResult = _authService.AuthorizeAction(HttpContext, @namespace, group, "config.history", configResource);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var config = await _configRepository.GetConfigAsync(@namespace, group, key, cancellationToken);
        if (config is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ConfigNotFound, "Config not found.", StatusCodes.Status404NotFound);
        }

        var history = await _configRepository.GetHistoryAsync(config.Id, cancellationToken);
        var response = history.Select(item => new ConfigHistoryResponse
        {
            Version = item.Version,
            Content = item.Content,
            ContentType = item.ContentType,
            PublishedAt = item.PublishedAt,
            PublishedBy = item.PublishedBy
        }).ToList();

        return Ok(response);
    }

    [HttpPost("rollback")]
    [ProducesResponseType(typeof(ConfigItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rollback(
        [FromBody] ConfigRollbackRequest request,
        CancellationToken cancellationToken)
    {
        var errors = ConfigRequestValidator.ValidateRollback(request);
        if (errors.Count > 0)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Invalid rollback request.", StatusCodes.Status400BadRequest, errors);
        }

        var configResource = $"config:{request.Namespace}/{request.Group}/{request.Key}";
        var authResult = _authService.AuthorizeAction(HttpContext, request.Namespace, request.Group, "config.rollback", configResource);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var config = await _configRepository.GetConfigAsync(request.Namespace, request.Group, request.Key, cancellationToken);
        if (config is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ConfigNotFound, "Config not found.", StatusCodes.Status404NotFound);
        }

        var history = await _configRepository.GetHistoryAsync(config.Id, cancellationToken);
        var target = history.FirstOrDefault(item => item.Version == request.Version);
        if (target is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ConfigHistoryNotFound, "Config history not found.", StatusCodes.Status404NotFound);
        }

        var beforeConfig = new
        {
            config.Namespace,
            config.Group,
            config.Key,
            config.ContentType,
            config.Version
        };

        var now = DateTimeOffset.UtcNow;
        var newVersion = config.Version + 1;
        var updated = new ConfigItemEntity
        {
            Id = config.Id,
            Namespace = config.Namespace,
            Group = config.Group,
            Key = config.Key,
            Content = target.Content,
            ContentType = target.ContentType,
            Version = newVersion,
            PublishedAt = now,
            PublishedBy = request.PublishedBy,
            CreatedAt = config.CreatedAt,
            UpdatedAt = now
        };

        await _configRepository.UpdateConfigAsync(updated, cancellationToken);
        await _configRepository.AddHistoryAsync(new ConfigHistoryEntity
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

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _notifier.Notify(ConfigHelper.BuildConfigKey(updated.Namespace, updated.Group, updated.Key));
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
        await _auditLogWriter.AddAsync(actor, "config.rollback", auditResource, beforeConfig, afterConfig, HttpContext.TraceIdentifier, cancellationToken);

        return Ok(ConfigHelper.ToConfigItemResponse(updated));
    }

    [HttpGet("list")]
    [ProducesResponseType(typeof(List<ConfigListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] string? @namespace,
        [FromQuery] string? group,
        CancellationToken cancellationToken)
    {
        IQueryable<ConfigItemEntity> query = _dbContext.ConfigItems.AsNoTracking();

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

        return Ok(items);
    }

    [HttpGet("subscribe")]
    [ProducesResponseType(typeof(ConfigItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Subscribe(
        [FromQuery] string @namespace,
        [FromQuery] string group,
        [FromQuery] string key,
        [FromQuery] int? timeoutMs,
        CancellationToken cancellationToken)
    {
        ConfigMetrics.RecordSubscribeRequest();

        var errors = ConfigRequestValidator.ValidateKey(@namespace, group, key);
        if (errors.Count > 0)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Namespace, group, and key are required.", StatusCodes.Status400BadRequest, errors);
        }

        var config = await _configRepository.GetConfigAsync(@namespace, group, key, cancellationToken);
        if (config is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ConfigNotFound, "Config not found.", StatusCodes.Status404NotFound);
        }

        var etagValue = ConfigHelper.BuildConfigEtag(config);
        if (!Request.Headers.TryGetValue("If-None-Match", out var ifNoneMatch) || ifNoneMatch != etagValue)
        {
            Response.Headers.ETag = etagValue;
            return Ok(ConfigHelper.ToConfigItemResponse(config));
        }

        var timeout = TimeSpan.FromMilliseconds(Math.Clamp(timeoutMs ?? 30000, 1000, 60000));
        var keyValue = ConfigHelper.BuildConfigKey(@namespace, group, key);
        var currentVersion = _notifier.GetVersion(keyValue);
        var changed = await _notifier.WaitForChangeAsync(keyValue, currentVersion, timeout, cancellationToken);

        if (!changed)
        {
            ConfigMetrics.RecordSubscribeTimeout();
            Response.Headers.ETag = etagValue;
            return StatusCode(StatusCodes.Status304NotModified);
        }

        ConfigMetrics.RecordSubscribeChange();

        config = await _configRepository.GetConfigAsync(@namespace, group, key, cancellationToken);
        if (config is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ConfigNotFound, "Config not found.", StatusCodes.Status404NotFound);
        }

        etagValue = ConfigHelper.BuildConfigEtag(config);
        Response.Headers.ETag = etagValue;
        return Ok(ConfigHelper.ToConfigItemResponse(config));
    }
}
