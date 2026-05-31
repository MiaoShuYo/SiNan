using System.Collections.Generic;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SiNan.Console.Pages.ApiKeys;

[Authorize]
public sealed class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public List<ApiKeyItem> ApiKeys { get; private set; } = [];
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.FindFirst("IsAdmin")?.Value != "true")
        {
            return Forbid();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            var data = await client.GetFromJsonAsync<List<ApiKeyItem>>("/api/v1/apikeys");
            ApiKeys = data ?? [];
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"无法加载 API 密钥列表: {ex.Message}";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id)
    {
        if (User.FindFirst("IsAdmin")?.Value != "true")
        {
            return Forbid();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            var response = await client.DeleteAsync($"/api/v1/apikeys/{id}");
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                ErrorMessage = $"删除失败: {(int)response.StatusCode} {response.ReasonPhrase} — {errorContent}";
                await OnGetAsync();
                return Page();
            }
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"删除失败: {ex.Message}";
            await OnGetAsync();
            return Page();
        }

        return RedirectToPage();
    }

    public sealed class ApiKeyItem
    {
        public System.Guid Id { get; set; }
        public string KeyMasked { get; set; } = string.Empty;
        public string Actor { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public string[] Namespaces { get; set; } = [];
        public string[] Groups { get; set; } = [];
        public string[] AllowedActions { get; set; } = [];
        public string[] AllowedResources { get; set; } = [];
        public bool Enabled { get; set; }
        public System.DateTimeOffset CreatedAt { get; set; }
        public System.DateTimeOffset UpdatedAt { get; set; }
    }
}
