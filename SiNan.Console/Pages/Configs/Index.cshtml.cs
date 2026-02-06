using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SiNan.Console.Pages.Configs;

public sealed class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [BindProperty(SupportsGet = true, Name = "namespace")]
    public string? Namespace { get; set; }

    [BindProperty(SupportsGet = true, Name = "group")]
    public string? Group { get; set; }

    public List<ConfigListItem> Configs { get; private set; } = new();

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            var query = new List<string>();

            if (!string.IsNullOrWhiteSpace(Namespace))
            {
                query.Add($"namespace={Uri.EscapeDataString(Namespace)}");
            }

            if (!string.IsNullOrWhiteSpace(Group))
            {
                query.Add($"group={Uri.EscapeDataString(Group)}");
            }

            var url = "/api/v1/configs/list";
            if (query.Count > 0)
            {
                url += "?" + string.Join("&", query);
            }

            var data = await client.GetFromJsonAsync<List<ConfigListItem>>(url);
            Configs = data ?? new List<ConfigListItem>();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"无法加载配置列表: {ex.Message}";
        }
    }

    public sealed class ConfigListItem
    {
        public string Namespace { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public int Version { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
