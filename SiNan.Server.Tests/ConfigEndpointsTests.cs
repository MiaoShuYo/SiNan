using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SiNan.Server.Tests;

public sealed class ConfigEndpointsTests : IClassFixture<SiNanWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ConfigEndpointsTests(SiNanWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Config_CRUD_And_History_Work()
    {
        var create = new
        {
            Namespace = "default",
            Group = "DEFAULT_GROUP",
            Key = "orders.timeout",
            Content = "1000",
            ContentType = "text/plain",
            PublishedBy = "system"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/configs", create);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var createPayload = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, createPayload.GetProperty("version").GetInt32());

        var getResponse = await _client.GetAsync("/api/v1/configs?namespace=default&group=DEFAULT_GROUP&key=orders.timeout");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var update = new
        {
            Namespace = "default",
            Group = "DEFAULT_GROUP",
            Key = "orders.timeout",
            Content = "1500",
            ContentType = "text/plain",
            PublishedBy = "system"
        };

        var updateResponse = await _client.PutAsJsonAsync("/api/v1/configs", update);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatePayload = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, updatePayload.GetProperty("version").GetInt32());

        var historyResponse = await _client.GetAsync("/api/v1/configs/history?namespace=default&group=DEFAULT_GROUP&key=orders.timeout");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var historyPayload = await historyResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(historyPayload.GetArrayLength() >= 2);
        Assert.Equal(2, historyPayload[0].GetProperty("version").GetInt32());

        var deleteResponse = await _client.DeleteAsync("/api/v1/configs?namespace=default&group=DEFAULT_GROUP&key=orders.timeout");
        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);

        var getMissing = await _client.GetAsync("/api/v1/configs?namespace=default&group=DEFAULT_GROUP&key=orders.timeout");
        Assert.Equal(HttpStatusCode.NotFound, getMissing.StatusCode);
    }

    [Fact]
    public async Task Config_Subscribe_Returns_On_Change()
    {
        var create = new
        {
            Namespace = "default",
            Group = "DEFAULT_GROUP",
            Key = "orders.retries",
            Content = "3",
            ContentType = "text/plain",
            PublishedBy = "system"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/configs", create);
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);

        var initialSubscribe = await _client.GetAsync("/api/v1/configs/subscribe?namespace=default&group=DEFAULT_GROUP&key=orders.retries");
        Assert.Equal(HttpStatusCode.OK, initialSubscribe.StatusCode);
        var etag = initialSubscribe.Headers.ETag?.Tag ?? string.Empty;

        var subscribeRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/configs/subscribe?namespace=default&group=DEFAULT_GROUP&key=orders.retries&timeoutMs=2000");
        subscribeRequest.Headers.TryAddWithoutValidation("If-None-Match", etag);

        var subscribeTask = _client.SendAsync(subscribeRequest);
        await Task.Delay(200);

        var update = new
        {
            Namespace = "default",
            Group = "DEFAULT_GROUP",
            Key = "orders.retries",
            Content = "5",
            ContentType = "text/plain",
            PublishedBy = "system"
        };

        var updateResponse = await _client.PutAsJsonAsync("/api/v1/configs", update);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var subscribeResponse = await subscribeTask;
        Assert.Equal(HttpStatusCode.OK, subscribeResponse.StatusCode);
    }
}
