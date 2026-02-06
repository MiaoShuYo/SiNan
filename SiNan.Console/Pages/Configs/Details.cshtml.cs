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

    [BindProperty(SupportsGet = true, Name = "token")]
    public string? Token { get; set; }

    [BindProperty(SupportsGet = true, Name = "publishedBy")]
    public string? PublishedBy { get; set; }

    [BindProperty]
    public string? ContentType { get; set; }

    [BindProperty]
    public string? Content { get; set; }

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

            if (Item is not null)
            {
                ContentType ??= Item.ContentType;
                Content ??= Item.Content;
            }
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"无法加载配置详情: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        if (string.IsNullOrWhiteSpace(Namespace) || string.IsNullOrWhiteSpace(Group) || string.IsNullOrWhiteSpace(Key))
        {
            ErrorMessage = "缺少必要参数。";
            return Page();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/configs");

            if (!string.IsNullOrWhiteSpace(Token))
            {
                request.Headers.TryAddWithoutValidation("X-SiNan-Token", Token);
            }

            request.Content = JsonContent.Create(new
            {
                Namespace,
                Group,
                Key,
                Content = Content ?? string.Empty,
                ContentType = ContentType,
                PublishedBy
            });

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = $"发布失败: {(int)response.StatusCode} {response.ReasonPhrase}";
                return Page();
            }

            return RedirectToPage(new
            {
                @namespace = Namespace,
                group = Group,
                key = Key,
                token = Token,
                publishedBy = PublishedBy
            });
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"发布失败: {ex.Message}";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostRollbackAsync(int version)
    {
        if (string.IsNullOrWhiteSpace(Namespace) || string.IsNullOrWhiteSpace(Group) || string.IsNullOrWhiteSpace(Key))
        {
            ErrorMessage = "缺少必要参数。";
            return Page();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            var url = $"/api/v1/configs/rollback?namespace={Uri.EscapeDataString(Namespace)}&group={Uri.EscapeDataString(Group)}&key={Uri.EscapeDataString(Key)}&version={version}";
            if (!string.IsNullOrWhiteSpace(PublishedBy))
            {
                url += $"&publishedBy={Uri.EscapeDataString(PublishedBy)}";
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            if (!string.IsNullOrWhiteSpace(Token))
            {
                request.Headers.TryAddWithoutValidation("X-SiNan-Token", Token);
            }

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = $"回滚失败: {(int)response.StatusCode} {response.ReasonPhrase}";
                return Page();
            }

            return RedirectToPage(new
            {
                @namespace = Namespace,
                group = Group,
                key = Key,
                token = Token,
                publishedBy = PublishedBy
            });
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"回滚失败: {ex.Message}";
            return Page();
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
