# SiNan SDK 使用文档

简体中文 | [English](sdk.en.md)

本文档面向使用 SiNan 的 .NET 开发者，覆盖 SDK 的安装、配置、鉴权、服务注册发现、配置管理、异常处理与最佳实践。

## 1. 安装

### 1.1 通过 NuGet

```bash
dotnet add package SiNan --version 1.0.0
```

### 1.2 通过项目引用（仓库内开发）

在你的项目中引用解决方案内的 `SiNan.SDK` 项目。

## 2. 基本概念

- **Server**: SiNan 后端 API 服务。默认地址常见为 `http://localhost:8080`（Docker）或 `http://localhost:5043`（本地）。
- **Console**: 管理控制台，与 SDK 无直接依赖。
- **Registry**: 服务注册与发现。
- **Config**: 配置中心。
- **API Key**: 通过请求头 `X-SiNan-Token` 传递的鉴权令牌（如启用鉴权）。

## 3. SDK 依赖注入与初始化

### 3.1 使用 DI（推荐）

```csharp
using SiNan.SDK;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSiNanClients(options =>
{
    options.BaseUrl = "http://localhost:8080"; // Server 地址
    options.Timeout = TimeSpan.FromSeconds(30); // 超时
    options.RetryCount = 2; // 请求重试次数
    options.RetryDelayMs = 200; // 初始重试延迟
    options.RetryMaxDelayMs = 2000; // 最大重试延迟
});

var app = builder.Build();
app.Run();
```

### 3.2 API Key 鉴权

当 Server 启用 API Key 时，将 `X-SiNan-Token` 加到请求头。

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

## 4. 服务注册与发现（Registry）

### 4.1 注册服务实例

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

### 4.2 心跳续约

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

### 4.3 注销实例

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

### 4.4 查询实例

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

### 4.5 订阅变更（长轮询 + ETag）

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

## 5. 配置管理（Config）

### 5.1 创建或更新配置

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

### 5.2 获取配置

```csharp
var item = await config.GetAsync(
    @namespace: "default",
    group: "DEFAULT_GROUP",
    key: "appsettings.json"
);
```

### 5.3 删除配置

```csharp
await config.DeleteAsync(
    @namespace: "default",
    group: "DEFAULT_GROUP",
    key: "appsettings.json"
);
```

### 5.4 回滚配置

```csharp
var rollback = await config.RollbackAsync(
    @namespace: "default",
    group: "DEFAULT_GROUP",
    key: "appsettings.json",
    version: 2,
    publishedBy: "admin"
);
```

### 5.5 查看历史

```csharp
var history = await config.GetHistoryAsync(
    @namespace: "default",
    group: "DEFAULT_GROUP",
    key: "appsettings.json"
);
```

### 5.6 配置订阅（长轮询 + ETag）

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

## 6. 异常与错误处理

SDK 在非 2xx 返回时会抛出 `ApiException`：

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

`GetInstancesAsync` / `SubscribeAsync` 等方法返回 `ApiResult<T>`，当服务未变化时：

- `NotModified = true`
- `Data = null`
- `ETag` 可能存在

## 7. 常见配置建议

- **超时**: 配置订阅通常设置 30s 或更长。
- **重试**: 注册/心跳可开启少量重试（默认 2 次）。
- **生产地址**: `BaseUrl` 指向你的 Server 公网/内网地址。
- **鉴权**: 生产建议启用 API Key，并仅授予必要权限。

## 8. 示例端点（Server）

- Registry: `/api/v1/registry/register` `/api/v1/registry/instances`
- Config: `/api/v1/configs` `/api/v1/configs/subscribe`
- Health: `/health`

如需更详细 API 结构，参阅：
- `docs/registry-api.md`
- `docs/config-api.md`
- `docs/audit-api.md`

## 9. 版本与兼容性

- SDK 目标框架：`net8.0`, `net10.0`
- Server 要求：SiNan Server 1.0.0 及以上

## 10. 常见问题

**Q: 控制台登录账号密码和 SDK 有关系吗？**
A: 没关系。Console 是管理 UI，SDK 调用 Server API。

**Q: 为什么返回 401？**
A: Server 开启了 API Key 校验，但请求未带 `X-SiNan-Token`。

**Q: 订阅接口一直挂起？**
A: 这是正常的长轮询行为，直到数据变更或超时返回。
