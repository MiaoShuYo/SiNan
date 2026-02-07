using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using SiNan.Console.Data;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddRazorPages();

builder.Services.AddDbContext<ConsoleAuthDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("ConsoleDb")
        ?? "Data Source=console-auth.db";
    options.UseSqlite(connectionString);
});

var jwtSection = builder.Configuration.GetSection("Auth:Jwt");
var jwtIssuer = jwtSection["Issuer"] ?? "SiNan.Console";
var jwtAudience = jwtSection["Audience"] ?? "SiNan.Console";
var jwtSigningKey = jwtSection["SigningKey"] ?? string.Empty;

if (jwtSigningKey.Length < 32)
{
    throw new InvalidOperationException("Auth:Jwt:SigningKey must be at least 32 characters.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var cookieName = builder.Configuration["Auth:CookieName"] ?? "sinan_auth";
                if (context.Request.Cookies.TryGetValue(cookieName, out var token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                if (!context.Handled)
                {
                    context.HandleResponse();
                    var returnUrl = context.Request.Path + context.Request.QueryString;
                    var loginUrl = $"/Account/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";
                    context.Response.Redirect(loginUrl);
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// Configure HttpClient with Aspire service discovery
// When running through Aspire, "http://sinan-server" will be resolved automatically
// When running standalone, use configured BaseUrl from appsettings.json
var siNanTimeoutSeconds = builder.Configuration.GetValue<int?>("SiNanServer:TimeoutSeconds") ?? 30;
var siNanTimeout = TimeSpan.FromSeconds(Math.Clamp(siNanTimeoutSeconds, 5, 300));
var siNanAttemptTimeout = TimeSpan.FromSeconds(Math.Clamp(siNanTimeout.TotalSeconds / 2, 5, 150));
var siNanSamplingDuration = TimeSpan.FromSeconds(Math.Max(30, siNanAttemptTimeout.TotalSeconds * 2));

builder.Services.Configure<HttpStandardResilienceOptions>(options =>
{
    options.TotalRequestTimeout.Timeout = siNanTimeout;
    options.AttemptTimeout.Timeout = siNanAttemptTimeout;
    options.CircuitBreaker.SamplingDuration = siNanSamplingDuration;
});

builder.Services.AddHttpClient("SiNanServer", client =>
{
    // Try to get service endpoint from Aspire service discovery first
    var serviceEndpoint = builder.Configuration["services:sinan-server:http:0"];
    
    // Fallback to configured BaseUrl or default
    var baseUrl = serviceEndpoint 
                  ?? builder.Configuration["SiNanServer:BaseUrl"] 
                  ?? "http://localhost:5043";

    if (builder.Environment.IsDevelopment()
        && baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        // Prefer HTTP in development to avoid untrusted dev certificates.
        baseUrl = "http://" + baseUrl["https://".Length..];
    }
    
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = siNanTimeout;
})
.AddStandardResilienceHandler(options =>
{
    options.TotalRequestTimeout.Timeout = siNanTimeout;
    options.AttemptTimeout.Timeout = siNanAttemptTimeout;
    options.CircuitBreaker.SamplingDuration = siNanSamplingDuration;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ConsoleAuthDbContext>();
    dbContext.Database.EnsureCreated();

    if (!dbContext.Users.Any())
    {
        var bootstrapSection = builder.Configuration.GetSection("Auth:BootstrapAdmin");
        var userName = bootstrapSection["UserName"] ?? "admin";
        var password = bootstrapSection["Password"] ?? "ChangeMe123!";

        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<AuthUser>();
        var user = new AuthUser
        {
            UserName = userName,
            CreatedAt = DateTime.UtcNow
        };

        user.PasswordHash = hasher.HashPassword(user, password);

        dbContext.Users.Add(user);
        dbContext.SaveChanges();
    }
}

var supportedCultures = new[] { "en-US", "zh-CN" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("zh-CN")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

localizationOptions.RequestCultureProviders = new List<IRequestCultureProvider>
{
    new QueryStringRequestCultureProvider(),
    new CookieRequestCultureProvider(),
    new AcceptLanguageHeaderRequestCultureProvider()
};

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseRequestLocalization(localizationOptions);

app.UseStaticFiles();

app.MapDefaultEndpoints();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/culture/set", (string culture, string? returnUrl, HttpContext httpContext) =>
{
    var resolvedCulture = supportedCultures.Contains(culture, StringComparer.OrdinalIgnoreCase)
        ? culture
        : localizationOptions.DefaultRequestCulture.Culture.Name;

    httpContext.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(resolvedCulture)),
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true
        });

    return Results.LocalRedirect(string.IsNullOrWhiteSpace(returnUrl) ? "/" : returnUrl);
}).AllowAnonymous();

app.MapStaticAssets().AllowAnonymous();
app.MapRazorPages();

app.Run();
