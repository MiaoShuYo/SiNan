using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Json;

namespace SiNan.Console.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly IHttpClientFactory _httpClientFactory;

    public LoginModel(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        IStringLocalizer<SharedResource> localizer,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _environment = environment;
        _localizer = localizer;
        _httpClientFactory = httpClientFactory;
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

        var client = _httpClientFactory.CreateClient("SiNanServer");
        var request = new ConsoleAuthLoginRequest
        {
            UserName = UserName,
            Password = Password
        };

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("/api/v1/console-auth/login", request);
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "登录超时，请确认服务器可用。";
            return Page();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"无法连接服务器: {ex.Message}";
            return Page();
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            ErrorMessage = _localizer["Auth.InvalidCredentials"];
            return Page();
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = $"登录失败: {(int)response.StatusCode} {response.ReasonPhrase}";
            return Page();
        }

        var loginResult = await response.Content.ReadFromJsonAsync<ConsoleAuthLoginResponse>();
        var userName = loginResult?.UserName ?? UserName;

        var token = CreateToken(userName);
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

    private sealed class ConsoleAuthLoginRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    private sealed class ConsoleAuthLoginResponse
    {
        public string UserName { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
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
