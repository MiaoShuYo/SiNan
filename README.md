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

## Notes
- Database support targets MySQL and SQLite. SQLite will be used for local/dev; MySQL for production.
- See requirements.md for detailed product requirements.
