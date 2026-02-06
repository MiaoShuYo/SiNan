using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SiNan.Console.Pages.Services;

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

    [BindProperty(SupportsGet = true, Name = "serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    public List<ServiceInstance> Instances { get; private set; } = new();

    public string? ErrorMessage { get; private set; }

    public async Task OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(Namespace) || string.IsNullOrWhiteSpace(Group) || string.IsNullOrWhiteSpace(ServiceName))
        {
            ErrorMessage = "缺少必要参数。";
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("SiNanServer");
            var url = $"/api/v1/registry/instances?namespace={Uri.EscapeDataString(Namespace)}&group={Uri.EscapeDataString(Group)}&serviceName={Uri.EscapeDataString(ServiceName)}&healthyOnly=false";
            var response = await client.GetFromJsonAsync<ServiceInstancesResponse>(url);
            Instances = response?.Instances ?? new List<ServiceInstance>();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"无法加载实例: {ex.Message}";
        }
    }

    public sealed class ServiceInstancesResponse
    {
        public List<ServiceInstance> Instances { get; set; } = new();
    }

    public sealed class ServiceInstance
    {
        public string InstanceId { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public int Weight { get; set; }
        public bool Healthy { get; set; }
        public int TtlSeconds { get; set; }
    }
}
