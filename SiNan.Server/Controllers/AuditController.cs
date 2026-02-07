using Microsoft.AspNetCore.Mvc;
using SiNan.Server.Auth;
using SiNan.Server.Contracts.Audit;
using SiNan.Server.Contracts.Common;
using SiNan.Server.Helpers;
using SiNan.Server.Storage;

namespace SiNan.Server.Controllers;

[ApiController]
[Route("api/v1/audit")]
public class AuditController : ControllerBase
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ApiKeyAuthorizationService _authService;

    public AuditController(
        IAuditLogRepository auditLogRepository,
        ApiKeyAuthorizationService authService)
    {
        _auditLogRepository = auditLogRepository;
        _authService = authService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(List<AuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Query(
        [FromQuery] int? take,
        [FromQuery] string? action,
        [FromQuery] string? resource,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var adminResult = _authService.AuthorizeAdmin(HttpContext);
        if (!adminResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, adminResult.Code!, adminResult.Message!, adminResult.StatusCode!.Value);
        }

        var authResult = _authService.AuthorizeAction(HttpContext, "system", "audit", "audit.read", "audit:logs");
        if (!authResult.Allowed)
        {
            return ErrorHelper.CreateError(HttpContext, authResult.Code!, authResult.Message!, authResult.StatusCode!.Value);
        }

        var limit = Math.Clamp(take ?? 100, 1, 500);
        var logs = await _auditLogRepository.QueryAsync(limit, action, resource, from, to, cancellationToken);
        var response = logs.Select(log => new AuditLogResponse
        {
            Actor = log.Actor,
            Action = log.Action,
            Resource = log.Resource,
            BeforeJson = log.BeforeJson,
            AfterJson = log.AfterJson,
            TraceId = log.TraceId,
            CreatedAt = log.CreatedAt
        }).ToList();

        return Ok(response);
    }
}
