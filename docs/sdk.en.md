# SiNan SDK Guide

English | [简体中文](sdk.md)

This guide targets .NET developers using SiNan. It covers installation, configuration, authentication, service registry & discovery, configuration management, error handling, and best practices.

## 1. Installation

### 1.1 NuGet

```bash
dotnet add package SiNan --version 1.0.0
```

### 1.2 Project Reference (monorepo)

Reference the `SiNan.SDK` project inside the solution.

## 2. Concepts

- **Server**: SiNan backend API. Common defaults are `http://localhost:8080` (Docker) or `http://localhost:5043` (local).
- **Console**: Management UI, not required by SDK.
- **Registry**: Service registration and discovery.
- **Config**: Configuration center.
- **API Key**: Auth token passed via `X-SiNan-Token` header (when enabled).

## 3. DI and Initialization

### 3.1 Use DI (recommended)

```csharp
using SiNan.SDK;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSiNanClients(options =>
{
    options.BaseUrl = "http://localhost:8080"; // Server base URL
    options.Timeout = TimeSpan.FromSeconds(30); // Timeout
    options.RetryCount = 2; // Retry count
    options.RetryDelayMs = 200; // Initial retry delay
    options.RetryMaxDelayMs = 2000; // Max retry delay
});

var app = builder.Build();
app.Run();
```

### 3.2 API Key Authentication

When API Key is enabled on Server, add `X-SiNan-Token` to request headers.

```csharp
builder.Services.AddHttpClient("SiNan")
    .ConfigureHttpClient(client =>
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-SiNan-Token",
            "your-api-key"
        );
    });
```

## 4. Service Registry & Discovery

### 4.1 Register an instance

```csharp
using SiNan.SDK.Registry;

app.MapPost("/demo/register", async (ISiNanRegistryClient registry) =>
{
    var response = await registry.RegisterAsync(new RegisterInstanceRequest
    {
        Namespace = "default",
        Group = "DEFAULT_GROUP",
        ServiceName = "demo-service",
        Host = "127.0.0.1",
        Port = 5001,
        Weight = 100,
        TtlSeconds = 30,
        IsEphemeral = true,
        Metadata = new Dictionary<string, string>
        {
            ["env"] = "dev",
            ["version"] = "1.0.0"
        }
    });

    return Results.Ok(response);
});
```

### 4.2 Heartbeat

```csharp
await registry.HeartbeatAsync(new HeartbeatRequest
{
    Namespace = "default",
    Group = "DEFAULT_GROUP",
    ServiceName = "demo-service",
    Host = "127.0.0.1",
    Port = 5001
});
```

### 4.3 Deregister

```csharp
await registry.DeregisterAsync(new DeregisterInstanceRequest
{
    Namespace = "default",
    Group = "DEFAULT_GROUP",
    ServiceName = "demo-service",
    Host = "127.0.0.1",
    Port = 5001
});
```

### 4.4 Query instances

```csharp
var result = await registry.GetInstancesAsync(
    @namespace: "default",
    group: "DEFAULT_GROUP",
    serviceName: "demo-service",
    healthyOnly: true
);

if (!result.NotModified && result.Data is not null)
{
    foreach (var item in result.Data.Instances)
    {
        Console.WriteLine($"{item.Host}:{item.Port} healthy={item.Healthy}");
    }
}
```

### 4.5 Subscribe (Long-polling + ETag)

```csharp
string? etag = null;

while (true)
{
    var res = await registry.SubscribeAsync(
        @namespace: "default",
        group: "DEFAULT_GROUP",
        serviceName: "demo-service",
        healthyOnly: true,
        timeoutMs: 30000,
        etag: etag
    );

    if (!res.NotModified && res.Data is not null)
    {
        etag = res.ETag;
        Console.WriteLine($"Changed, instances = {res.Data.Instances.Count}");
    }
}
```

## 5. Configuration Management

### 5.1 Create or update config

```csharp
using SiNan.SDK.Config;

var created = await config.CreateAsync(new ConfigUpsertRequest
{
    Namespace = "default",
    Group = "DEFAULT_GROUP",
    Key = "appsettings.json",
    Content = "{\"Logging\":{\"LogLevel\":{\"Default\":\"Information\"}}}",
    ContentType = "application/json",
    PublishedBy = "admin"
});
```

```csharp
var updated = await config.UpdateAsync(new ConfigUpsertRequest
{
    Namespace = "default",
    Group = "DEFAULT_GROUP",
    Key = "appsettings.json",
    Content = "{\"Logging\":{\"LogLevel\":{\"Default\":\"Warning\"}}}",
    ContentType = "application/json",
    PublishedBy = "admin"
});
```

### 5.2 Get config

```csharp
var item = await config.GetAsync(
    @namespace: "default",
    group: "DEFAULT_GROUP",
    key: "appsettings.json"
);
```

### 5.3 Delete config

```csharp
await config.DeleteAsync(
    @namespace: "default",
    group: "DEFAULT_GROUP",
    key: "appsettings.json"
);
```

### 5.4 Rollback

```csharp
var rollback = await config.RollbackAsync(
    @namespace: "default",
    group: "DEFAULT_GROUP",
    key: "appsettings.json",
    version: 2,
    publishedBy: "admin"
);
```

### 5.5 History

```csharp
var history = await config.GetHistoryAsync(
    @namespace: "default",
    group: "DEFAULT_GROUP",
    key: "appsettings.json"
);
```

### 5.6 Subscribe (Long-polling + ETag)

```csharp
string? etag = null;

while (true)
{
    var res = await config.SubscribeAsync(
        @namespace: "default",
        group: "DEFAULT_GROUP",
        key: "appsettings.json",
        timeoutMs: 30000,
        etag: etag
    );

    if (!res.NotModified && res.Data is not null)
    {
        etag = res.ETag;
        Console.WriteLine($"Config updated to version {res.Data.Version}");
    }
}
```

## 6. Errors and Exceptions

SDK throws `ApiException` for non-2xx responses:

```csharp
try
{
    var item = await config.GetAsync("default", "DEFAULT_GROUP", "missing-key");
}
catch (ApiException ex)
{
    Console.WriteLine($"Status: {ex.StatusCode}");
    Console.WriteLine(ex.Message);
}
```

For `GetInstancesAsync` / `SubscribeAsync`, the SDK returns `ApiResult<T>`. When nothing changes:

- `NotModified = true`
- `Data = null`
- `ETag` may be present

## 7. Recommended Settings

- **Timeout**: For subscriptions, 30s or more is typical.
- **Retry**: Small retry count for register/heartbeat is recommended.
- **Production URL**: Set `BaseUrl` to your production Server URL.
- **Auth**: Enable API Key in production and grant least privilege.

## 8. Example Endpoints (Server)

- Registry: `/api/v1/registry/register` `/api/v1/registry/instances`
- Config: `/api/v1/configs` `/api/v1/configs/subscribe`
- Health: `/health`

For detailed API structure, see:
- `docs/registry-api.md`
- `docs/config-api.md`
- `docs/audit-api.md`

## 9. Versions and Compatibility

- SDK targets: `net8.0`, `net10.0`
- Server: SiNan Server 1.0.0+

## 10. FAQ

**Q: Does Console login affect SDK?**
A: No. Console is only the UI. SDK talks to Server API.

**Q: Why do I get 401?**
A: API Key is enabled on Server, but `X-SiNan-Token` was not provided.

**Q: Subscription request hangs?**
A: It is expected behavior for long-polling until data changes or timeout.
