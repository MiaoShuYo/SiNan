using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SiNan.Console.Pages.Configs;

public sealed class CreateModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public CreateModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [BindProperty(SupportsGet = true, Name = "namespace")]
    public string Namespace { get; set; } = "default";

    [BindProperty(SupportsGet = true, Name = "group")]
    public string Group { get; set; } = "DEFAULT_GROUP";

    [BindProperty]
    public string Key { get; set; } = string.Empty;

    [BindProperty]
    public string Content { get; set; } = string.Empty;

    [BindProperty]
    public string ContentType { get; set; } = "text/plain";

    [BindProperty]
    public string? PublishedBy { get; set; }

    [BindProperty]
    public string? Token { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Key) || string.IsNullOrWhiteSpace(Content))
        {
            ErrorMessage = "Key 和 Content 不能为空。";
            return Page();
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/configs");
            if (!string.IsNullOrWhiteSpace(Token))
            {
                request.Headers.TryAddWithoutValidation("X-SiNan-Token", Token);
            }

            request.Content = JsonContent.Create(new
            {
                Namespace,
                Group,
                Key,
                Content,
                ContentType,
                PublishedBy
            });

            var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = $"创建失败: {(int)response.StatusCode} {response.ReasonPhrase}";
                return Page();
            }

            return RedirectToPage("/Configs/Details", new
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
            ErrorMessage = $"创建失败: {ex.Message}";
            return Page();
        }
    }
}
