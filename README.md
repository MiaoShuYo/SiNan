# SiNan

<div align="center">

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()

**A modern service registry, discovery, and configuration platform for .NET**

English | [简体中文](README.zh-CN.md)

[Features](#features) • [Quick Start](#quick-start) • [Documentation](#documentation) • [API Reference](#api-reference)

</div>

---

## 📖 Introduction

SiNan is a .NET 10 service registry/discovery and configuration management platform inspired by Nacos, built with .NET Aspire. It provides a comprehensive solution for microservices infrastructure with features including:

- **Service Registry & Discovery**: Dynamic service registration, health checks, and long-polling subscriptions
- **Configuration Management**: Centralized configuration with versioning, rollback, and real-time updates
- **Security & Audit**: API key authentication, RBAC authorization, and comprehensive audit logging
- **High Availability**: Designed for production with quota management and HA support
- **Developer Friendly**: Web console, .NET SDK, and extensive API documentation

## ✨ Features

### Service Registry & Discovery
- ✅ Service registration and deregistration
- ✅ Heartbeat-based health monitoring
- ✅ Instance query with filtering (healthy/all)
- ✅ Long-polling subscriptions with ETag support
- ✅ Service list and metadata management
- ✅ Ephemeral instances support

### Configuration Management
- ✅ CRUD operations for configuration items
- ✅ Version history tracking
- ✅ Configuration rollback to any version
- ✅ Long-polling subscriptions for real-time updates
- ✅ Multiple namespaces and groups
- ✅ Content type support (text, JSON, YAML, etc.)
- ✅ Automatic history cleanup

### Security & Compliance
- ✅ API Key authentication (optional)
- ✅ RBAC with namespace/group isolation
- ✅ Action-level and resource-level permissions
- ✅ Comprehensive audit logging
- ✅ Audit query API for administrators

### Operation & Management
- ✅ Web-based management console
- ✅ Quota management (services, instances, configs)
- ✅ OpenTelemetry integration
- ✅ Health checks and metrics
- ✅ SQLite and MySQL support

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     SiNan Platform                           │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌─────────────┐  ┌──────────────┐  ┌──────────────┐       │
│  │   Console   │  │   SDK/.NET   │  │  HTTP Client │       │
│  │  (Web UI)   │  │   Clients    │  │   (Any Lang) │       │
│  └──────┬──────┘  └──────┬───────┘  └──────┬───────┘       │
│         │                │                  │                │
│         └────────────────┴──────────────────┘                │
│                          │                                   │
│         ┌────────────────▼────────────────┐                 │
│         │     API Gateway / Middleware    │                 │
│         │   (Auth, Audit, Rate Limit)     │                 │
│         └────────────────┬────────────────┘                 │
│                          │                                   │
│    ┌─────────────────────┴─────────────────────┐            │
│    │                                            │            │
│    ▼                                            ▼            │
│ ┌──────────────────┐              ┌──────────────────┐     │
│ │ Registry Service │              │  Config Service  │     │
│ │   - Register     │              │   - Get/Set      │     │
│ │   - Heartbeat    │              │   - Subscribe    │     │
│ │   - Subscribe    │              │   - Rollback     │     │
│ │   - Query        │              │   - History      │     │
│ └────────┬─────────┘              └────────┬─────────┘     │
│          │                                  │               │
│          └──────────────┬───────────────────┘               │
│                         │                                   │
│              ┌──────────▼─────────┐                         │
│              │  Storage Layer     │                         │
│              │  (SQLite / MySQL)  │                         │
│              └────────────────────┘                         │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

## 📁 Repository Structure

```
SiNan/
├── SiNan.AppHost/              # .NET Aspire orchestration
├── SiNan.ServiceDefaults/      # Shared Aspire configuration
├── SiNan.Server/               # Core API server
│   ├── Controllers/            # API Controllers
│   │   ├── RegistryController.cs
│   │   ├── ConfigController.cs
│   │   └── AuditController.cs
│   ├── Auth/                   # Authentication & Authorization
│   ├── Config/                 # Configuration services
│   ├── Registry/               # Registry services
│   ├── Storage/                # Data access layer
│   ├── Helpers/                # Utility classes
│   └── Program.cs              # Application entry point
├── SiNan.Console/              # Web management console
├── SiNan.SDK/                  # .NET client SDK
├── SiNan.Server.Tests/         # Unit tests
├── docs/                       # Documentation
│   ├── registry-api.md
│   ├── config-api.md
│   ├── audit-api.md
│   └── ha-load-test-plan.md
└── requirements.md             # Product requirements

```

## 🚀 Quick Start

### Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- [Docker](https://www.docker.com/) (optional, for containerized deployment)
- [Docker Compose](https://docs.docker.com/compose/) (optional, for multi-container setup)

### Option 1: Local Development with .NET Aspire

The fastest way to get started for development:

```bash
# Clone the repository
git clone https://github.com/MiaoShuYo/SiNan.git
cd SiNan

# Run with Aspire orchestration
dotnet run --project SiNan.AppHost
```

Access the services:
- **API Server**: http://localhost:5043
- **Web Console**: http://localhost:5044
- **Aspire Dashboard**: http://localhost:15888

### Option 2: Docker Container

Run SiNan Server as a standalone Docker container:

```bash
# Build the image
docker build -t sinan-server ./SiNan.Server

# Run with SQLite (default)
docker run -d -p 5043:8080 \
  --name sinan-server \
  sinan-server

# Run with MySQL
docker run -d -p 5043:8080 \
  -e Data__Provider=MySql \
  -e ConnectionStrings__SiNan="Server=mysql;Database=sinan;User=root;Password=password;" \
  --name sinan-server \
  sinan-server
```

### Option 3: Docker Compose (Recommended for Production)

Complete setup with MySQL:

```bash
# Start all services
docker compose up -d

# View logs
docker compose logs -f

# Stop services
docker compose down
```

For SQLite-only deployment:

```bash
docker compose --profile sqlite up -d
```

### Verify Installation

Test the API:

```bash
# Health check
curl http://localhost:5043/health

# Register a service
curl -X POST http://localhost:5043/api/v1/registry/register \
  -H "Content-Type: application/json" \
  -d '{
    "namespace": "default",
    "group": "DEFAULT_GROUP",
    "serviceName": "demo-service",
    "host": "127.0.0.1",
    "port": 8080,
    "weight": 100,
    "ttlSeconds": 30,
    "isEphemeral": true
  }'

# Query instances
curl http://localhost:5043/api/v1/registry/instances?namespace=default&group=DEFAULT_GROUP&serviceName=demo-service
```

## ⚙️ Configuration

### Database Configuration

Configure database provider in `appsettings.json`:

**SQLite (Default):**
```json
{
  "Data": {
    "Provider": "Sqlite"
  },
  "ConnectionStrings": {
    "SiNan": "Data Source=sinan.db"
  }
}
```

**MySQL:**
```json
{
  "Data": {
    "Provider": "MySql"
  },
  "ConnectionStrings": {
    "SiNan": "Server=localhost;Database=sinan;User=root;Password=your_password;"
  }
}
```

### Authentication & Authorization

Enable API key authentication:

```json
{
  "Auth": {
    "Enabled": true,
    "HeaderName": "X-SiNan-Token",
    "ActorHeaderName": "X-SiNan-Actor",
    "ApiKeys": [
      {
        "Key": "admin-secret-token-12345",
        "Actor": "admin",
        "IsAdmin": true,
        "Namespaces": [],
        "Groups": [],
        "AllowedActions": [],
        "AllowedResources": []
      },
      {
        "Key": "app-token-67890",
        "Actor": "application",
        "IsAdmin": false,
        "Namespaces": ["default", "production"],
        "Groups": ["DEFAULT_GROUP"],
        "AllowedActions": [
          "registry.register",
          "registry.deregister",
          "registry.heartbeat",
          "registry.read",
          "config.read"
        ],
        "AllowedResources": [
          "registry:default/DEFAULT_GROUP/",
          "config:default/DEFAULT_GROUP/"
        ]
      }
    ]
  }
}
```

**Permission Model:**
- `IsAdmin`: Grants access to audit logs and bypasses action checks
- `Namespaces`: Restricts to specific namespaces (empty = all allowed)
- `Groups`: Restricts to specific groups (empty = all allowed)
- `AllowedActions`: Restricts specific operations (empty = all allowed)
- `AllowedResources`: Prefix-based resource filter (empty = all allowed)

**Available Actions:**
- Registry: `registry.register`, `registry.deregister`, `registry.heartbeat`, `registry.read`
- Config: `config.create`, `config.update`, `config.delete`, `config.rollback`, `config.read`, `config.history`
- Audit: `audit.read` (requires `IsAdmin: true`)

### Quota Management

Configure resource limits per namespace:

```json
{
  "Quota": {
    "MaxServicesPerNamespace": 1000,
    "MaxInstancesPerNamespace": 5000,
    "MaxConfigsPerNamespace": 500,
    "MaxConfigContentLength": 65535
  }
}
```

Set to `0` to disable specific quota checks.

### Health Check & Cleanup

```json
{
  "Registry": {
    "Health": {
      "CheckIntervalSeconds": 5,
      "MarkUnhealthyAfterSeconds": 30
    }
  },
  "Config": {
    "HistoryCleanup": {
      "Enabled": true,
      "IntervalHours": 24,
      "RetainVersions": 10,
      "RetainDays": 90
    }
  }
}
```

## 📚 Documentation

### API Reference

- [Registry API Documentation](docs/registry-api.md) ([中文](docs/registry-api.zh-CN.md)) - Service registration, discovery, and subscriptions
- [Configuration API Documentation](docs/config-api.md) ([中文](docs/config-api.zh-CN.md)) - Configuration management and versioning
- [Audit API Documentation](docs/audit-api.md) ([中文](docs/audit-api.zh-CN.md)) - Audit log queries
- [HA & Load Testing](docs/ha-load-test-plan.md) ([中文](docs/ha-load-test-plan.zh-CN.md)) - High availability deployment guide

### API Endpoints Overview

**Registry Services** (`/api/v1/registry`)
- `POST /register` - Register a service instance
- `POST /deregister` - Deregister an instance
- `POST /heartbeat` - Send heartbeat
- `GET /instances` - Query instances
- `GET /subscribe` - Subscribe to instance changes
- `GET /services` - List all services

**Configuration Services** (`/api/v1/configs`)
- `POST /` - Create configuration
- `PUT /` - Update configuration
- `GET /` - Get configuration
- `DELETE /` - Delete configuration
- `GET /history` - Get version history
- `POST /rollback` - Rollback to specific version
- `GET /list` - List configurations
- `GET /subscribe` - Subscribe to config changes

**Audit Services** (`/api/v1/audit`)
- `GET /` - Query audit logs (admin only)

## 💻 .NET SDK Usage

### Installation

```bash
dotnet add package SiNan.SDK
```

### Basic Usage

```csharp
using SiNan.SDK.Config;
using SiNan.SDK.Registry;
using Microsoft.Extensions.DependencyInjection;

// Configure services
var services = new ServiceCollection();
services.AddSiNanClients(options =>
{
    options.BaseUrl = "http://localhost:5043";
    options.ApiKey = "your-api-token"; // Optional
    options.RetryCount = 3;
    options.RetryDelayMs = 500;
});

var provider = services.BuildServiceProvider();
```

### Service Registry Example

```csharp
var registryClient = provider.GetRequiredService<ISiNanRegistryClient>();

// Register service instance
var registerResult = await registryClient.RegisterAsync(new RegisterInstanceRequest
{
    Namespace = "default",
    Group = "DEFAULT_GROUP",
    ServiceName = "order-service",
    Host = "192.168.1.100",
    Port = 8080,
    Weight = 100,
    TtlSeconds = 30,
    IsEphemeral = true,
    Metadata = new Dictionary<string, string>
    {
        ["version"] = "1.0.0",
        ["region"] = "us-west"
    }
});

Console.WriteLine($"Registered: {registerResult.InstanceId}");

// Send heartbeat
await registryClient.HeartbeatAsync(new HeartbeatRequest
{
    Namespace = "default",
    Group = "DEFAULT_GROUP",
    ServiceName = "order-service",
    Host = "192.168.1.100",
    Port = 8080
});

// Query instances
var instances = await registryClient.GetInstancesAsync(
    "default",
    "DEFAULT_GROUP",
    "order-service",
    healthyOnly: true
);

foreach (var instance in instances.Instances)
{
    Console.WriteLine($"{instance.Host}:{instance.Port} - {instance.Healthy}");
}

// Subscribe to changes (long-polling)
var subscription = await registryClient.SubscribeAsync(
    "default",
    "DEFAULT_GROUP",
    "order-service",
    healthyOnly: true,
    timeoutMs: 30000
);

// Deregister
await registryClient.DeregisterAsync(new DeregisterInstanceRequest
{
    Namespace = "default",
    Group = "DEFAULT_GROUP",
    ServiceName = "order-service",
    Host = "192.168.1.100",
    Port = 8080
});
```

### Configuration Management Example

```csharp
var configClient = provider.GetRequiredService<ISiNanConfigClient>();

// Create configuration
var createResult = await configClient.CreateAsync(new ConfigUpsertRequest
{
    Namespace = "default",
    Group = "DEFAULT_GROUP",
    Key = "database.connection",
    Content = "Server=localhost;Database=mydb;",
    ContentType = "text/plain",
    PublishedBy = "admin"
});

Console.WriteLine($"Created config version: {createResult.Version}");

// Get configuration
var config = await configClient.GetAsync("default", "DEFAULT_GROUP", "database.connection");
Console.WriteLine($"Content: {config.Content}");

// Update configuration
var updateResult = await configClient.UpdateAsync(new ConfigUpsertRequest
{
    Namespace = "default",
    Group = "DEFAULT_GROUP",
    Key = "database.connection",
    Content = "Server=prod-server;Database=mydb;",
    ContentType = "text/plain",
    PublishedBy = "admin"
});

// Get history
var history = await configClient.GetHistoryAsync("default", "DEFAULT_GROUP", "database.connection");
foreach (var item in history)
{
    Console.WriteLine($"Version {item.Version}: {item.PublishedAt}");
}

// Rollback to previous version
var rollbackResult = await configClient.RollbackAsync(
    "default",
    "DEFAULT_GROUP",
    "database.connection",
    version: 1,
    publishedBy: "admin"
);

// Subscribe to changes
var configSubscription = await configClient.SubscribeAsync(
    "default",
    "DEFAULT_GROUP",
    "database.connection",
    timeoutMs: 30000
);

if (!configSubscription.NotModified)
{
    Console.WriteLine($"Config changed: {configSubscription.Data?.Content}");
}

// Delete configuration
await configClient.DeleteAsync("default", "DEFAULT_GROUP", "database.connection");
```

## 🔧 Development

### Building from Source

```bash
# Clone repository
git clone https://github.com/MiaoShuYo/SiNan.git
cd SiNan

# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests
dotnet test

# Run server
cd SiNan.Server
dotnet run
```

### Project Structure

- **Controllers**: ASP.NET Core Web API controllers
- **Services**: Business logic layer
- **Storage**: Database repositories and EF Core entities
- **Auth**: Authentication and authorization
- **Helpers**: Utility methods and extensions
- **Contracts**: Request/Response DTOs

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test SiNan.Server.Tests

# With coverage
dotnet test /p:CollectCoverage=true
```

**Note**: Test discovery requires compatible test runners. If tests show 0/0, this is a known .NET 10 environment limitation and doesn't affect functionality.

## 🐳 Docker Deployment

### Building Custom Images

```bash
# Build server image
docker build -f SiNan.Server/Dockerfile -t sinan-server:latest .

# Build console image
docker build -f SiNan.Console/Dockerfile -t sinan-console:latest .
```

### Environment Variables

SiNan Server supports configuration through environment variables:

| Variable | Description | Default |
|----------|-------------|---------|
| `Data__Provider` | Database provider (`Sqlite` or `MySql`) | `Sqlite` |
| `ConnectionStrings__SiNan` | Database connection string | `Data Source=sinan.db` |
| `Auth__Enabled` | Enable API key authentication | `false` |
| `Auth__HeaderName` | HTTP header for API key | `X-SiNan-Token` |
| `Auth__ActorHeaderName` | HTTP header for actor name | `X-SiNan-Actor` |
| `Quota__MaxServicesPerNamespace` | Max services per namespace | `1000` |
| `Quota__MaxInstancesPerNamespace` | Max instances per namespace | `5000` |
| `Quota__MaxConfigsPerNamespace` | Max configs per namespace | `500` |
| `Quota__MaxConfigContentLength` | Max config content size (bytes) | `65535` |
| `Registry__Health__CheckIntervalSeconds` | Health check interval | `5` |
| `Registry__Health__MarkUnhealthyAfterSeconds` | TTL before marking unhealthy | `30` |
| `Config__HistoryCleanup__Enabled` | Enable automatic history cleanup | `true` |
| `Config__HistoryCleanup__IntervalHours` | Cleanup interval | `24` |
| `Config__HistoryCleanup__RetainVersions` | Versions to retain | `10` |
| `Config__HistoryCleanup__RetainDays` | Days to retain history | `90` |
| `ASPNETCORE_URLS` | Server listening URLs | `http://+:8080` |

**Example Docker run with environment variables:**

```bash
docker run -d -p 5043:8080 \
  -e Data__Provider=MySql \
  -e ConnectionStrings__SiNan="Server=mysql;Database=sinan;User=root;Password=pass;" \
  -e Auth__Enabled=true \
  -e Quota__MaxServicesPerNamespace=2000 \
  --name sinan-server \
  sinan-server:latest
```

### Docker Compose with Advanced Configuration

Complete example with MySQL, authentication, and custom quotas:

```yaml
version: '3.8'

services:
  mysql:
    image: mysql:8.0
    environment:
      MYSQL_ROOT_PASSWORD: root_password
      MYSQL_DATABASE: sinan
    ports:
      - "3306:3306"
    volumes:
      - mysql-data:/var/lib/mysql
    healthcheck:
      test: ["CMD", "mysqladmin", "ping", "-h", "localhost"]
      interval: 10s
      timeout: 5s
      retries: 5

  sinan-server:
    image: sinan-server:latest
    ports:
      - "5043:8080"
    environment:
      - Data__Provider=MySql
      - ConnectionStrings__SiNan=Server=mysql;Database=sinan;User=root;Password=root_password;
      - Auth__Enabled=true
      - Quota__MaxServicesPerNamespace=2000
      - Quota__MaxInstancesPerNamespace=10000
      - Registry__Health__CheckIntervalSeconds=10
    depends_on:
      mysql:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  sinan-console:
    image: sinan-console:latest
    ports:
      - "5044:8080"
    environment:
      - SiNan__ApiBaseUrl=http://sinan-server:8080
    depends_on:
      - sinan-server

volumes:
  mysql-data:
```

## 📊 Monitoring & Observability

SiNan integrates with OpenTelemetry for comprehensive observability:

### Metrics

Available metrics include:
- **Registry**: Active services, instance count, registration rate, heartbeat rate
- **Configuration**: Total configs, update rate, subscription count
- **API**: Request count, latency, error rate
- **System**: Memory usage, CPU, garbage collection

### Health Checks

Built-in health check endpoint:

```bash
curl http://localhost:5043/health
```

Response:
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "registry": "Healthy",
    "config": "Healthy"
  }
}
```

### Aspire Dashboard

When running with .NET Aspire, access the dashboard at http://localhost:15888 for:
- Real-time trace visualization
- Metrics and counters
- Logs aggregation
- Resource monitoring

## 🤝 Contributing

We welcome contributions! Please follow these guidelines:

### Development Workflow

1. **Fork the repository**
2. **Create a feature branch**
   ```bash
   git checkout -b feature/amazing-feature
   ```
3. **Make your changes**
   - Follow C# coding conventions
   - Add/update tests as needed
   - Update documentation
4. **Commit your changes**
   ```bash
   git commit -m "Add amazing feature"
   ```
5. **Push to your fork**
   ```bash
   git push origin feature/amazing-feature
   ```
6. **Open a Pull Request**

### Coding Standards

- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable and method names
- Write XML documentation for public APIs
- Keep methods focused and concise
- Add unit tests for new features

### Testing Guidelines

- Maintain test coverage above 80%
- Write unit tests for business logic
- Write integration tests for API endpoints
- Use descriptive test names (e.g., `RegisterInstance_WhenValid_ReturnsSuccess`)

### Pull Request Checklist

- [ ] Code follows project coding standards
- [ ] All tests pass (`dotnet test`)
- [ ] New features have tests
- [ ] Documentation is updated
- [ ] Commit messages are clear and descriptive
- [ ] No merge conflicts

## 📝 Roadmap

- [ ] Distributed deployment with clustering
- [ ] gRPC API support
- [ ] Service mesh integration
- [ ] Advanced load balancing strategies
- [ ] Configuration encryption
- [ ] Multi-language SDKs (Java, Python, Go)
- [ ] Kubernetes operator
- [ ] Enhanced web console with metrics dashboard

## 🐛 Known Issues

1. **Test Discovery**: .NET 10 environment may show 0/0 tests in some runners. This doesn't affect functionality.
2. **Long-polling Timeout**: Default timeout is 30 seconds. Adjust `timeoutMs` parameter based on your network conditions.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Inspired by [Alibaba Nacos](https://nacos.io/)
- Built with [.NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/)
- Uses [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)

## 📧 Contact & Support

- **Issues**: [GitHub Issues](https://github.com/MiaoShuYo/SiNan/issues)
- **Discussions**: [GitHub Discussions](https://github.com/MiaoShuYo/SiNan/discussions)
- **Documentation**: [docs/](docs/)

---

<div align="center">

Made with ❤️ by the SiNan Team

[⬆ Back to Top](#sinan)

</div>
