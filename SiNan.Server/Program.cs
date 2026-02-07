/// <summary>
/// SiNan server application entry point
/// Configures services, middleware, and database connections
/// </summary>

using Microsoft.EntityFrameworkCore;
using SiNan.Server.Audit;
using SiNan.Server.Auth;
using SiNan.Server.Config;
using SiNan.Server.Data;
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
        // Use MySQL 8.0
        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)));
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

// Enable HTTPS redirection
app.UseHttpsRedirection();

// Add API key authentication middleware
app.UseMiddleware<ApiKeyAuthMiddleware>();

// Map controller routes
app.MapControllers();
// Map default endpoints (health checks, etc.)
app.MapDefaultEndpoints();

app.Run();

public partial class Program
{
}
