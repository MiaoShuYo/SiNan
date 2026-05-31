using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SiNan.Console.Pages.ApiKeys;

[Authorize]
public sealed class CreateModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public CreateModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [BindProperty]
    public string Key { get; set; } = string.Empty;

    [BindProperty]
    public string Actor { get; set; } = string.Empty;

    [BindProperty]
    public bool IsAdmin { get; set; }

    [BindProperty]
    public string Namespaces { get; set; } = string.Empty;

    [BindProperty]
    public string Groups { get; set; } = string.Empty;

    [BindProperty]
    public string AllowedActions { get; set; } = string.Empty;

    [BindProperty]
    public string AllowedResources { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (User.FindFirst("IsAdmin")?.Value != "true")
        {
            return Forbid();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.FindFirst("IsAdmin")?.Value != "true")
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(Key) || Key.Length < 8)
        {
            ErrorMessage = "密钥至少需要 8 个字符。";
            return Page();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/apikeys");

            request.Content = JsonContent.Create(new
            {
                key = Key,
                actor = Actor,
                isAdmin = IsAdmin,
                namespaces = ParseCsv(Namespaces),
                groups = ParseCsv(Groups),
                allowedActions = ParseCsv(AllowedActions),
                allowedResources = ParseCsv(AllowedResources)
            });

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                ErrorMessage = $"创建失败: {(int)response.StatusCode} {response.ReasonPhrase}";
                try
                {
                    using var doc = JsonDocument.Parse(errorContent);
                    var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        ErrorMessage += $" — {msg}";
                    }
                }
                catch { /* ignore parse error */ }
                return Page();
            }

            return RedirectToPage("/ApiKeys/Index");
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"无法连接服务器: {ex.Message}";
            return Page();
        }
    }

    private static string[] ParseCsv(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return [];
        }

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
