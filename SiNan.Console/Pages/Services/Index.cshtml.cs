using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SiNan.Console.Pages.Services;

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

    public List<ServiceSummary> Services { get; private set; } = new();

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

            var url = "/api/v1/registry/services";
            if (query.Count > 0)
            {
                url += "?" + string.Join("&", query);
            }

            var data = await client.GetFromJsonAsync<List<ServiceSummary>>(url);
            Services = data ?? new List<ServiceSummary>();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"无法连接到服务端: {ex.Message}";
        }
    }

    public sealed class ServiceSummary
    {
        public string Namespace { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public int InstanceCount { get; set; }
        public int HealthyInstanceCount { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
