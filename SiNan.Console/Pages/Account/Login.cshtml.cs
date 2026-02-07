using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using SiNan.Console.Data;

namespace SiNan.Console.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly ConsoleAuthDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public LoginModel(
        ConsoleAuthDbContext dbContext,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IStringLocalizer<SharedResource> localizer)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _environment = environment;
        _localizer = localizer;
    }

    [BindProperty]
    public string UserName { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = _localizer["Auth.MissingCredentials"];
            return Page();
        }

        var user = await _dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserName == UserName);

        if (user is null)
        {
            ErrorMessage = _localizer["Auth.InvalidCredentials"];
            return Page();
        }

        var hasher = new PasswordHasher<AuthUser>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, Password);
        if (result == PasswordVerificationResult.Failed)
        {
            ErrorMessage = _localizer["Auth.InvalidCredentials"];
            return Page();
        }

        var token = CreateToken(user.UserName);
        var cookieName = _configuration["Auth:CookieName"] ?? "sinan_auth";
        var expiresMinutes = int.TryParse(_configuration["Auth:Jwt:ExpiresMinutes"], out var minutes)
            ? minutes
            : 60;

        Response.Cookies.Append(cookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMinutes(expiresMinutes)
        });

        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return LocalRedirect(ReturnUrl);
        }

        return RedirectToPage("/Index");
    }

    private string CreateToken(string userName)
    {
        var issuer = _configuration["Auth:Jwt:Issuer"] ?? "SiNan.Console";
        var audience = _configuration["Auth:Jwt:Audience"] ?? "SiNan.Console";
        var signingKey = _configuration["Auth:Jwt:SigningKey"] ?? string.Empty;
        var expiresMinutes = int.TryParse(_configuration["Auth:Jwt:ExpiresMinutes"], out var minutes)
            ? minutes
            : 60;

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userName),
            new Claim(ClaimTypes.Name, userName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiresMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
