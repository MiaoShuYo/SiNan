var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorPages();

// Configure HttpClient with Aspire service discovery
// When running through Aspire, "http://sinan-server" will be resolved automatically
// When running standalone, use configured BaseUrl from appsettings.json
builder.Services.AddHttpClient("SiNanServer", client =>
{
    // Try to get service endpoint from Aspire service discovery first
    var serviceEndpoint = builder.Configuration["services:sinan-server:http:0"];
    
    // Fallback to configured BaseUrl or default
    var baseUrl = serviceEndpoint 
                  ?? builder.Configuration["SiNanServer:BaseUrl"] 
                  ?? "http://localhost:5043";
    
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
