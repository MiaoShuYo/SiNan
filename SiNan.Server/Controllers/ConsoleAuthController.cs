/// <summary>
/// Console authentication controller
/// Validates console login credentials against server database
/// </summary>
/// 
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiNan.Server.Contracts.Common;
using SiNan.Server.Data;
using SiNan.Server.Data.Entities;
using SiNan.Server.Helpers;

namespace SiNan.Server.Controllers;

[ApiController]
[Route("api/v1/console-auth")]
public sealed class ConsoleAuthController : ControllerBase
{
    private readonly SiNanDbContext _dbContext;

    public ConsoleAuthController(SiNanDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ConsoleAuthLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] ConsoleAuthLoginRequest request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.ValidationFailed, "Invalid credentials payload.", StatusCodes.Status400BadRequest);
        }

        var user = await _dbContext.ConsoleUsers.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == request.UserName, cancellationToken);

        if (user is null)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.Unauthorized, "Invalid username or password.", StatusCodes.Status401Unauthorized);
        }

        var hasher = new PasswordHasher<ConsoleUserEntity>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return ErrorHelper.CreateError(HttpContext, ErrorCodes.Unauthorized, "Invalid username or password.", StatusCodes.Status401Unauthorized);
        }

        return Ok(new ConsoleAuthLoginResponse
        {
            UserName = user.UserName,
            IsAdmin = user.IsAdmin
        });
    }

    public sealed class ConsoleAuthLoginRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public sealed class ConsoleAuthLoginResponse
    {
        public string UserName { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
    }
}
