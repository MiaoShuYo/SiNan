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

## Auth (optional)
Set `Auth__Enabled` to `true` and configure API tokens in `Auth__ApiKeys`.
Send the token via `X-SiNan-Token` header. Optional actor header: `X-SiNan-Actor`.

Admin keys set `IsAdmin=true` and can access audit query endpoint: `GET /api/v1/audit?take=100`.

RBAC filters:
- `AllowedActions` restricts operations (e.g. `config.create`, `config.update`, `config.delete`, `config.rollback`, `config.read`, `config.history`, `registry.register`, `registry.deregister`, `registry.heartbeat`, `registry.read`, `audit.read`).
- `AllowedResources` uses prefix matching (e.g. `config:default/DEFAULT_GROUP/` or `registry:default/DEFAULT_GROUP/orders`).

Middleware behavior:
- When auth is enabled, write requests (`POST/PUT/PATCH/DELETE`) and `/api/v1/audit` require `X-SiNan-Token`.
- Action-level checks still run inside handlers for namespace/group and action/resource rules.

## Notes
- Database support targets MySQL and SQLite. SQLite will be used for local/dev; MySQL for production.
- See requirements.md for detailed product requirements.
- Registry API reference: docs/registry-api.md
- Config API reference: docs/config-api.md
- HA/load test plan: docs/ha-load-test-plan.md

## Quotas (optional)
Configure namespace-level limits in `Quota`:
- `MaxServicesPerNamespace`, `MaxInstancesPerNamespace`, `MaxConfigsPerNamespace`, `MaxConfigContentLength`.
Set to `0` to disable.
## .NET SDK (preview)
```csharp
using SiNan.SDK.Config;
using SiNan.SDK.Registry;

services.AddSiNanClients(options =>
{
	options.BaseUrl = "http://localhost:5043";
	options.RetryCount = 2;
	options.RetryDelayMs = 200;
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
