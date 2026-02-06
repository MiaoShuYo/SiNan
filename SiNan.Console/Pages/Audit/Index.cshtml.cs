using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SiNan.Console.Pages.Audit;

public sealed class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [BindProperty(SupportsGet = true, Name = "token")]
    public string? Token { get; set; }

    [BindProperty(SupportsGet = true, Name = "action")]
    public string? Action { get; set; }

    [BindProperty(SupportsGet = true, Name = "resource")]
    public string? Resource { get; set; }

    [BindProperty(SupportsGet = true, Name = "take")]
    public int Take { get; set; } = 100;

    [BindProperty(SupportsGet = true, Name = "from")]
    public string? From { get; set; }

    [BindProperty(SupportsGet = true, Name = "to")]
    public string? To { get; set; }

    public List<AuditLogItem> Logs { get; private set; } = new();

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            var query = new List<string>
            {
                $"take={Math.Clamp(Take, 1, 500)}"
            };

            if (!string.IsNullOrWhiteSpace(Action))
            {
                query.Add($"action={Uri.EscapeDataString(Action)}");
            }

            if (!string.IsNullOrWhiteSpace(Resource))
            {
                query.Add($"resource={Uri.EscapeDataString(Resource)}");
            }

            if (!string.IsNullOrWhiteSpace(From))
            {
                query.Add($"from={Uri.EscapeDataString(From)}");
            }

            if (!string.IsNullOrWhiteSpace(To))
            {
                query.Add($"to={Uri.EscapeDataString(To)}");
            }

            var url = "/api/v1/audit?" + string.Join("&", query);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrWhiteSpace(Token))
            {
                request.Headers.TryAddWithoutValidation("X-SiNan-Token", Token);
            }

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = $"请求失败: {(int)response.StatusCode} {response.ReasonPhrase}";
                return;
            }

            var data = await response.Content.ReadFromJsonAsync<List<AuditLogItem>>();
            Logs = data ?? new List<AuditLogItem>();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"无法加载审计日志: {ex.Message}";
        }
    }

    public sealed class AuditLogItem
    {
        public string Actor { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Resource { get; set; } = string.Empty;
        public string? TraceId { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
