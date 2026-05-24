using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SiNan.Console.Pages.Configs;

public sealed class HistoryModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HistoryModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string Namespace { get; private set; } = string.Empty;
    public string Group { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;

    public List<ConfigHistoryItem> History { get; private set; } = new();

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync(string @namespace, string group, string key)
    {
        Namespace = @namespace ?? string.Empty;
        Group = group ?? string.Empty;
        Key = key ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Namespace) || string.IsNullOrWhiteSpace(Group) || string.IsNullOrWhiteSpace(Key))
        {
            ErrorMessage = "缺少必要参数。";
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            var query = $"namespace={Uri.EscapeDataString(Namespace)}&group={Uri.EscapeDataString(Group)}&key={Uri.EscapeDataString(Key)}";
            var history = await client.GetFromJsonAsync<List<ConfigHistoryItem>>($"/api/v1/configs/history?{query}");
            History = history ?? new List<ConfigHistoryItem>();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"无法加载版本历史: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostRollbackAsync(string @namespace, string group, string key, int version)
    {
        Namespace = @namespace ?? string.Empty;
        Group = group ?? string.Empty;
        Key = key ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Namespace) || string.IsNullOrWhiteSpace(Group) || string.IsNullOrWhiteSpace(Key))
        {
            ErrorMessage = "缺少必要参数。";
            return Page();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/configs/rollback");
            request.Content = JsonContent.Create(new
            {
                Namespace,
                Group,
                Key,
                Version = version,
                PublishedBy = User.Identity?.Name
            });

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = $"回滚失败: {(int)response.StatusCode} {response.ReasonPhrase}";
                await OnGetAsync(Namespace, Group, Key);
                return Page();
            }

            return RedirectToPage("/Configs/Details", new { @namespace = Namespace, group = Group, key = Key });
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"回滚失败: {ex.Message}";
            await OnGetAsync(Namespace, Group, Key);
            return Page();
        }
    }

    public sealed class ConfigHistoryItem
    {
        public int Version { get; set; }
        public DateTimeOffset PublishedAt { get; set; }
        public string? PublishedBy { get; set; }
    }
}
