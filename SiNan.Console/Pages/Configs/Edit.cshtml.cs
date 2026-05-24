using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SiNan.Console.Pages.Configs;

public sealed class EditModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public EditModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [BindProperty(SupportsGet = true, Name = "namespace")]
    public string Namespace { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true, Name = "group")]
    public string Group { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true, Name = "key")]
    public string Key { get; set; } = string.Empty;

    [BindProperty]
    public string ContentType { get; set; } = "TEXT";

    [BindProperty]
    public new string Content { get; set; } = string.Empty;

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Namespace) || string.IsNullOrWhiteSpace(Group) || string.IsNullOrWhiteSpace(Key))
            return;

        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            var query = $"namespace={Uri.EscapeDataString(Namespace)}&group={Uri.EscapeDataString(Group)}&key={Uri.EscapeDataString(Key)}";
            var item = await client.GetFromJsonAsync<ConfigItem>($"/api/v1/configs?{query}");
            if (item is not null)
            {
                ContentType = item.ContentType;
                Content = item.Content;
            }
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"无法加载配置: {ex.Message}";
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
            request.Content = JsonContent.Create(new
            {
                Namespace,
                Group,
                Key,
                Content,
                ContentType,
                PublishedBy = User.Identity?.Name
            });

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = $"发布失败: {(int)response.StatusCode} {response.ReasonPhrase}";
                return Page();
            }

            return RedirectToPage("/Configs/Details", new { @namespace = Namespace, group = Group, key = Key });
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"发布失败: {ex.Message}";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync()
    {
        if (string.IsNullOrWhiteSpace(Namespace) || string.IsNullOrWhiteSpace(Group) || string.IsNullOrWhiteSpace(Key))
        {
            ErrorMessage = "缺少必要参数。";
            return Page();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/configs");
            request.Content = JsonContent.Create(new { Namespace, Group, Key });

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = $"删除失败: {(int)response.StatusCode} {response.ReasonPhrase}";
                return Page();
            }

            return RedirectToPage("/Configs/Index", new { @namespace = Namespace, group = Group });
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"删除失败: {ex.Message}";
            return Page();
        }
    }

    private sealed class ConfigItem
    {
        public string Content { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
}
