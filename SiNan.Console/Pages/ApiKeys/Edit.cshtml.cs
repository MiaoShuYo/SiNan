using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SiNan.Console.Pages.ApiKeys;

[Authorize]
public sealed class EditModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public EditModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [BindProperty(SupportsGet = true)]
    public Guid Id { get; set; }

    public string KeyMasked { get; set; } = string.Empty;

    [BindProperty]
    public string Actor { get; set; } = string.Empty;

    [BindProperty]
    public bool IsAdmin { get; set; }

    [BindProperty]
    public bool Enabled { get; set; } = true;

    [BindProperty]
    public string Namespaces { get; set; } = string.Empty;

    [BindProperty]
    public string Groups { get; set; } = string.Empty;

    [BindProperty]
    public string AllowedActions { get; set; } = string.Empty;

    [BindProperty]
    public string AllowedResources { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.FindFirst("IsAdmin")?.Value != "true")
        {
            return Forbid();
        }

        if (Id == Guid.Empty)
        {
            return NotFound();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            var item = await client.GetFromJsonAsync<ApiKeyDetail>($"/api/v1/apikeys/{Id}");
            if (item is null)
            {
                return NotFound();
            }

            KeyMasked = item.KeyMasked;
            Actor = item.Actor;
            IsAdmin = item.IsAdmin;
            Enabled = item.Enabled;
            Namespaces = string.Join(", ", item.Namespaces);
            Groups = string.Join(", ", item.Groups);
            AllowedActions = string.Join(", ", item.AllowedActions);
            AllowedResources = string.Join(", ", item.AllowedResources);
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"无法加载 API 密钥: {ex.Message}";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.FindFirst("IsAdmin")?.Value != "true")
        {
            return Forbid();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/apikeys/{Id}");

            request.Content = JsonContent.Create(new
            {
                actor = Actor,
                isAdmin = IsAdmin,
                enabled = Enabled,
                namespaces = ParseCsv(Namespaces),
                groups = ParseCsv(Groups),
                allowedActions = ParseCsv(AllowedActions),
                allowedResources = ParseCsv(AllowedResources)
            });

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                ErrorMessage = $"更新失败: {(int)response.StatusCode} {response.ReasonPhrase}";
                try
                {
                    using var doc = JsonDocument.Parse(errorContent);
                    var msg = doc.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(msg))
                    {
                        ErrorMessage += $" — {msg}";
                    }
                }
                catch { }
                // Reload the data so the form is still populated
                await OnGetAsync();
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

    public sealed class ApiKeyDetail
    {
        public Guid Id { get; set; }
        public string KeyMasked { get; set; } = string.Empty;
        public string Actor { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public string[] Namespaces { get; set; } = [];
        public string[] Groups { get; set; } = [];
        public string[] AllowedActions { get; set; } = [];
        public string[] AllowedResources { get; set; } = [];
        public bool Enabled { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
