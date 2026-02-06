using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SiNan.Console.Pages.Configs;

public sealed class DetailsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DetailsModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [BindProperty(SupportsGet = true, Name = "namespace")]
    public string Namespace { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true, Name = "group")]
    public string Group { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true, Name = "key")]
    public string Key { get; set; } = string.Empty;

    public ConfigItem? Item { get; private set; }

    public List<ConfigHistoryItem> History { get; private set; } = new();

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Namespace) || string.IsNullOrWhiteSpace(Group) || string.IsNullOrWhiteSpace(Key))
        {
            ErrorMessage = "缺少必要参数。";
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            var query = $"namespace={Uri.EscapeDataString(Namespace)}&group={Uri.EscapeDataString(Group)}&key={Uri.EscapeDataString(Key)}";

            Item = await client.GetFromJsonAsync<ConfigItem>($"/api/v1/configs?{query}");
            var history = await client.GetFromJsonAsync<List<ConfigHistoryItem>>($"/api/v1/configs/history?{query}");
            History = history ?? new List<ConfigHistoryItem>();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"无法加载配置详情: {ex.Message}";
        }
    }

    public sealed class ConfigItem
    {
        public string Content { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public int Version { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public string? PublishedBy { get; set; }
    }

    public sealed class ConfigHistoryItem
    {
        public int Version { get; set; }
        public DateTimeOffset PublishedAt { get; set; }
        public string? PublishedBy { get; set; }
    }
}
