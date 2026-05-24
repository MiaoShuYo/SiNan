using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SiNan.Console.Pages.Configs;

public sealed class DetailsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DetailsModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public string Namespace { get; private set; } = string.Empty;
    public string Group { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;

    public ConfigItem? Item { get; private set; }

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
            Item = await client.GetFromJsonAsync<ConfigItem>($"/api/v1/configs?{query}");
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
}
