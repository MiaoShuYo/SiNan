# SiNan

SiNan is a .NET 10 service registry/discovery and configuration platform (inspired by Nacos), built with .NET Aspire.

## Repo Structure
- SiNan.AppHost: Aspire AppHost (local orchestration)
- SiNan.ServiceDefaults: Shared Aspire defaults (OTel, health, service discovery)
- SiNan.Server: Core API for registry/discovery and config management
- SiNan.Console: Web console (Razor Pages starter)
- SiNan.SDK: .NET client SDK (placeholder)

## Prerequisites
- .NET SDK 10.0
- Docker (for MySQL and container deployment)

## Local Development (Aspire)
```bash
dotnet run --project SiNan.AppHost
```

## Docker (server only)
```bash
docker build -t sinan-server ./SiNan.Server
```

## Docker Compose
```bash
docker compose up -d
```

SQLite-only profile:
```bash
docker compose --profile sqlite up -d
```

## Configuration
- `Data__Provider`: `Sqlite` (default) or `MySql`
- `ConnectionStrings__SiNan`: provider-specific connection string

## Notes
- Database support targets MySQL and SQLite. SQLite will be used for local/dev; MySQL for production.
- See requirements.md for detailed product requirements.
- Registry API reference: docs/registry-api.md
- Config API reference: docs/config-api.md

## .NET SDK (preview)
```csharp
using Microsoft.Extensions.DependencyInjection;
using SiNan.SDK;
using SiNan.SDK.Config;
using SiNan.SDK.Registry;

var services = new ServiceCollection();
services.AddSiNanClients(options =>
{
	options.BaseUrl = "http://localhost:5043";
});

var provider = services.BuildServiceProvider();
var registryClient = provider.GetRequiredService<ISiNanRegistryClient>();
var configClient = provider.GetRequiredService<ISiNanConfigClient>();

await registryClient.RegisterAsync(new RegisterInstanceRequest
{
	Namespace = "default",
	Group = "DEFAULT_GROUP",
	ServiceName = "orders",
	Host = "127.0.0.1",
	Port = 8080
});

var config = await configClient.GetAsync("default", "DEFAULT_GROUP", "orders.timeout");
```
