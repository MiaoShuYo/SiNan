using System.Net.Http.Json;
using System.Text.Json;

namespace SiNan.SDK;

internal static class HttpJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static HttpContent CreateJsonContent<T>(T payload)
    {
        return JsonContent.Create(payload, options: Options);
    }

    public static Task<HttpResponseMessage> PostAsync<T>(HttpClient client, string path, T payload, CancellationToken cancellationToken)
    {
        return client.PostAsJsonAsync(path, payload, Options, cancellationToken);
    }

    public static Task<HttpResponseMessage> PutAsync<T>(HttpClient client, string path, T payload, CancellationToken cancellationToken)
    {
        return client.PutAsJsonAsync(path, payload, Options, cancellationToken);
    }

    public static async Task<T?> ReadAsync<T>(HttpContent content, CancellationToken cancellationToken)
    {
        return await content.ReadFromJsonAsync<T>(Options, cancellationToken);
    }
}
