/// <summary>
/// SiNan server application entry point
/// Configures services, middleware, and database connections
/// </summary>

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SiNan.Server.Audit;
using SiNan.Server.Auth;
using SiNan.Server.Config;
using SiNan.Server.Data;
using SiNan.Server.Data.Entities;
using SiNan.Server.Quotas;
using SiNan.Server.Registry;
using SiNan.Server.Storage;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, etc.)
builder.AddServiceDefaults();

// Get database configuration: provider (SQLite or MySQL) and connection string
var provider = builder.Configuration.GetValue<string>("Data:Provider") ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("SiNan") ?? "Data Source=sinan.db";

// Configure database context: choose MySQL or SQLite based on configuration
builder.Services.AddDbContext<SiNanDbContext>(options =>
{
    if (provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
    {
        // Use MySQL 8.0 with retry on failure
        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)),
            mySqlOptions => mySqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorNumbersToAdd: null));
    }
    else
    {
        // Default to SQLite
        options.UseSqlite(connectionString);
    }
});

// Register storage layer services (scoped: one instance per request)
builder.Services.AddScoped<IServiceRegistryRepository, EfServiceRegistryRepository>();
builder.Services.AddScoped<IConfigRepository, EfConfigRepository>();
builder.Services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<AuditLogWriter>();

// Register change notification services (singleton: unique per application lifetime)
builder.Services.AddSingleton<RegistryChangeNotifier>();
builder.Services.AddSingleton<ConfigChangeNotifier>();

// Configure and register registry health monitoring background service
builder.Services.Configure<RegistryHealthOptions>(builder.Configuration.GetSection("Registry:Health"));
builder.Services.AddHostedService<RegistryHealthMonitor>();

// Configure and register config history cleanup background service
builder.Services.Configure<ConfigHistoryCleanupOptions>(builder.Configuration.GetSection("Config:HistoryCleanup"));
builder.Services.AddHostedService<ConfigHistoryCleanupService>();

// Configure authentication and authorization services
builder.Services.Configure<ApiKeyAuthOptions>(builder.Configuration.GetSection("Auth"));
builder.Services.AddSingleton<ApiKeyAuthorizationService>();

// Configure resource quota options
builder.Services.Configure<QuotaOptions>(builder.Configuration.GetSection("Quota"));

// Add controller support
builder.Services.AddControllers();
// Add OpenAPI support (for API documentation generation)
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply database migrations automatically
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SiNanDbContext>();
    try
    {
        // Apply pending migrations
        dbContext.Database.Migrate();
        app.Logger.LogInformation("Database migrations applied successfully.");

        var bootstrapSection = builder.Configuration.GetSection("ConsoleAuth:BootstrapAdmin");
        var userName = bootstrapSection["UserName"] ?? "admin";
        var password = bootstrapSection["Password"] ?? "ChangeMe123!";

        var userExists = dbContext.ConsoleUsers.Any(u => u.UserName == userName);
        if (!userExists)
        {
            var hasher = new PasswordHasher<ConsoleUserEntity>();
            var user = new ConsoleUserEntity
            {
                Id = Guid.NewGuid(),
                UserName = userName,
                IsAdmin = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            user.PasswordHash = hasher.HashPassword(user, password);
            dbContext.ConsoleUsers.Add(user);
            dbContext.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred while applying database migrations.");
        throw;
    }
}

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    // Enable OpenAPI endpoint in development environment
    app.MapOpenApi();
}

// Enable HTTPS redirection only outside development
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Add API key authentication middleware
app.UseMiddleware<ApiKeyAuthMiddleware>();

// Map root endpoint
app.MapGet("/", (HttpContext context) =>
{
    var accept = context.Request.Headers.Accept.ToString();
    var isHtmlRequest = accept.Contains("text/html");

    if (isHtmlRequest)
    {
        var html = $@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>SiNan - Service Registry &amp; Configuration Platform</title>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 0; background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); min-height: 100vh; display: flex; align-items: center; justify-content: center; }}
        .container {{ background: white; border-radius: 12px; padding: 3rem; box-shadow: 0 20px 60px rgba(0,0,0,0.3); max-width: 600px; width: 90%; }}
        h1 {{ color: #667eea; margin: 0 0 0.5rem 0; font-size: 2.5rem; }}
        .version {{ color: #999; font-size: 0.9rem; margin-bottom: 1.5rem; }}
        .description {{ color: #666; margin-bottom: 2rem; line-height: 1.6; }}
        .endpoints {{ background: #f8f9fa; border-radius: 8px; padding: 1.5rem; }}
        .endpoints h2 {{ color: #333; margin: 0 0 1rem 0; font-size: 1.2rem; }}
        .endpoint-list {{ list-style: none; padding: 0; margin: 0; }}
        .endpoint-list li {{ margin-bottom: 0.75rem; }}
        .endpoint-list a {{ color: #667eea; text-decoration: none; font-family: 'Courier New', monospace; display: inline-block; padding: 0.5rem 1rem; background: white; border-radius: 4px; transition: all 0.2s; }}
        .endpoint-list a:hover {{ background: #667eea; color: white; transform: translateX(5px); }}
        .status {{ margin-top: 2rem; padding-top: 1.5rem; border-top: 1px solid #e0e0e0; color: #999; font-size: 0.85rem; }}
        .status-dot {{ display: inline-block; width: 8px; height: 8px; background: #48bb78; border-radius: 50%; margin-right: 0.5rem; animation: pulse 2s infinite; }}
        @keyframes pulse {{ 0% {{ opacity: 1; }} 50% {{ opacity: 0.5; }} 100% {{ opacity: 1; }} }}
    </style>
</head>
<body>
    <div class='container'>
        <h1>🧭 SiNan</h1>
        <div class='version'>Version 1.0.0</div>
        <div class='description'>
            A modern service registry, discovery, and configuration management platform for distributed systems. Built with .NET 10 and inspired by Nacos.
        </div>
        <div class='endpoints'>
            <h2>📡 API Endpoints</h2>
            <ul class='endpoint-list'>
                <li><a href='/health'>/health</a> - Health check</li>
                <li><a href='/alive'>/alive</a> - Liveness probe</li>
                {(app.Environment.IsDevelopment() ? "<li><a href='/openapi/v1.json'>/openapi/v1.json</a> - OpenAPI specification</li>" : "")}
                <li><a href='/api/services'>/api/services</a> - Service registry</li>
                <li><a href='/api/configs'>/api/configs</a> - Configuration management</li>
                <li><a href='/api/audit'>/api/audit</a> - Audit logs</li>
            </ul>
        </div>
        <div class='status'>
            <span class='status-dot'></span>
            Server is running • {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
        </div>
    </div>
</body>
</html>";
        return Results.Content(html, "text/html; charset=utf-8");
    }

    return Results.Json(new
    {
        name = "SiNan",
        version = "1.0.0",
        description = "Service Registry, Discovery and Configuration Management Platform",
        endpoints = new
        {
            health = "/health",
            alive = "/alive",
            openapi = app.Environment.IsDevelopment() ? "/openapi/v1.json" : null,
            api = new
            {
                services = "/api/services",
                configs = "/api/configs",
                audit = "/api/audit"
            }
        },
        timestamp = DateTimeOffset.UtcNow
    });
});

// Map controller routes
app.MapControllers();
// Map default endpoints (health checks, etc.)
app.MapDefaultEndpoints();

app.Run();

public partial class Program
{
}
