using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SiNan.Console.Pages.Account;

[Authorize]
public class LogoutModel : PageModel
{
    private readonly IConfiguration _configuration;

    public LogoutModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IActionResult OnPost()
    {
        var cookieName = _configuration["Auth:CookieName"] ?? "sinan_auth";
        Response.Cookies.Delete(cookieName);
        return RedirectToPage("/Account/Login");
    }
}
