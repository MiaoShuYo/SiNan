using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SiNan.Server.Auth;
using SiNan.Server.Audit;
using SiNan.Server.Contracts.ApiKeys;
using SiNan.Server.Contracts.Common;
using SiNan.Server.Data;
using SiNan.Server.Data.Entities;
using SiNan.Server.Helpers;
using SiNan.Server.Storage;

namespace SiNan.Server.Controllers;

[ApiController]
[Route("api/v1/apikeys")]
public sealed class ApiKeyController : ControllerBase
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly ApiKeyAuthorizationService _authService;
    private readonly AuditLogWriter _auditLogWriter;
    private readonly SiNanDbContext _dbContext;

    public ApiKeyController(
        IApiKeyRepository apiKeyRepository,
        ApiKeyAuthorizationService authService,
        AuditLogWriter auditLogWriter,
        SiNanDbContext dbContext)
    {
        _apiKeyRepository = apiKeyRepository;
        _authService = authService;
        _auditLogWriter = auditLogWriter;
        _dbContext = dbContext;
    }

    /// <summary>
    /// List all API keys (masked).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiKeyResponse[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var authResult = _authService.AuthorizeAdmin(HttpContext);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var keys = await _apiKeyRepository.GetAllAsync(cancellationToken);
        var response = keys.Select(ToResponse).ToArray();
        return Ok(response);
    }

    /// <summary>
    /// Get a single API key by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiKeyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var authResult = _authService.AuthorizeAdmin(HttpContext);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var key = await _apiKeyRepository.GetByIdAsync(id, cancellationToken);
        if (key is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ApiKeyNotFound, "API key not found.", StatusCodes.Status404NotFound);
        }

        return Ok(ToResponse(key));
    }

    /// <summary>
    /// Create a new API key.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiKeyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] ApiKeyCreateRequest request, CancellationToken cancellationToken)
    {
        var authResult = _authService.AuthorizeAdmin(HttpContext);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Key is required.", StatusCodes.Status400BadRequest);
        }

        if (request.Key.Length < 8)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Key must be at least 8 characters.", StatusCodes.Status400BadRequest);
        }

        // Check for duplicate key
        var existing = await _apiKeyRepository.GetByKeyAsync(request.Key, cancellationToken);
        if (existing is not null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ApiKeyAlreadyExists, "An API key with this value already exists.", StatusCodes.Status409Conflict);
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new ApiKeyEntity
        {
            Id = Guid.NewGuid(),
            Key = request.Key,
            Actor = string.IsNullOrWhiteSpace(request.Actor) ? "unknown" : request.Actor,
            IsAdmin = request.IsAdmin,
            NamespacesJson = ApiKeyJson.SerializeList(request.Namespaces),
            GroupsJson = ApiKeyJson.SerializeList(request.Groups),
            AllowedActionsJson = ApiKeyJson.SerializeList(request.AllowedActions),
            AllowedResourcesJson = ApiKeyJson.SerializeList(request.AllowedResources),
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _apiKeyRepository.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache so the new key is immediately usable
        ApiKeyAuthorizationService.InvalidateCache();

        // Audit log
        var actor = authResult.Actor;
        await _auditLogWriter.AddAsync(
            actor,
            "apikey.create",
            $"apikey:{entity.Id}",
            null,
            new { entity.Actor, entity.IsAdmin, KeyMasked = MaskKey(entity.Key) },
            HttpContext.TraceIdentifier,
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToResponse(entity));
    }

    /// <summary>
    /// Update an existing API key (key value cannot be changed).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiKeyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] ApiKeyUpdateRequest request, CancellationToken cancellationToken)
    {
        var authResult = _authService.AuthorizeAdmin(HttpContext);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var entity = await _apiKeyRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ApiKeyNotFound, "API key not found.", StatusCodes.Status404NotFound);
        }

        var before = new { entity.Actor, entity.IsAdmin, entity.Enabled, entity.NamespacesJson, entity.GroupsJson, entity.AllowedActionsJson, entity.AllowedResourcesJson };

        entity.Actor = string.IsNullOrWhiteSpace(request.Actor) ? "unknown" : request.Actor;
        entity.IsAdmin = request.IsAdmin;
        entity.Enabled = request.Enabled;
        entity.NamespacesJson = ApiKeyJson.SerializeList(request.Namespaces);
        entity.GroupsJson = ApiKeyJson.SerializeList(request.Groups);
        entity.AllowedActionsJson = ApiKeyJson.SerializeList(request.AllowedActions);
        entity.AllowedResourcesJson = ApiKeyJson.SerializeList(request.AllowedResources);
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _apiKeyRepository.UpdateAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache so changes take effect immediately
        ApiKeyAuthorizationService.InvalidateCache(entity.Key);

        await _auditLogWriter.AddAsync(
            authResult.Actor,
            "apikey.update",
            $"apikey:{entity.Id}",
            before,
            new { entity.Actor, entity.IsAdmin, entity.Enabled, entity.NamespacesJson, entity.GroupsJson, entity.AllowedActionsJson, entity.AllowedResourcesJson },
            HttpContext.TraceIdentifier,
            cancellationToken);

        return Ok(ToResponse(entity));
    }

    /// <summary>
    /// Delete an API key. Cannot delete the last admin key.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var authResult = _authService.AuthorizeAdmin(HttpContext);
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var entity = await _apiKeyRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ApiKeyNotFound, "API key not found.", StatusCodes.Status404NotFound);
        }

        // Prevent deleting the last admin key
        if (entity.IsAdmin)
        {
            var allKeys = await _apiKeyRepository.GetAllAsync(cancellationToken);
            var adminCount = allKeys.Count(k => k.IsAdmin);
            if (adminCount <= 1)
            {
                return ErrorHelper.CreateError(HttpContext, ErrorCodes.CannotDeleteLastAdminKey,
                    "Cannot delete the last admin API key. Create another admin key first.", StatusCodes.Status400BadRequest);
            }
        }

        await _apiKeyRepository.DeleteAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        ApiKeyAuthorizationService.InvalidateCache(entity.Key);

        await _auditLogWriter.AddAsync(
            authResult.Actor,
            "apikey.delete",
            $"apikey:{entity.Id}",
            new { entity.Actor, entity.IsAdmin, KeyMasked = MaskKey(entity.Key) },
            null,
            HttpContext.TraceIdentifier,
            cancellationToken);

        return Ok();
    }

    private static ApiKeyResponse ToResponse(ApiKeyEntity entity)
    {
        return new ApiKeyResponse
        {
            Id = entity.Id,
            KeyMasked = MaskKey(entity.Key),
            Actor = entity.Actor,
            IsAdmin = entity.IsAdmin,
            Namespaces = ApiKeyJson.DeserializeList(entity.NamespacesJson).ToArray(),
            Groups = ApiKeyJson.DeserializeList(entity.GroupsJson).ToArray(),
            AllowedActions = ApiKeyJson.DeserializeList(entity.AllowedActionsJson).ToArray(),
            AllowedResources = ApiKeyJson.DeserializeList(entity.AllowedResourcesJson).ToArray(),
            Enabled = entity.Enabled,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    /// <summary>
    /// Mask a key for display: show first 4 and last 4 characters.
    /// </summary>
    private static string MaskKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length <= 8)
        {
            return new string('*', Math.Min(key.Length, 8));
        }

        return key[..4] + new string('*', key.Length - 8) + key[^4..];
    }
}
