using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SiNan.SDK.Config;
using SiNan.SDK.Registry;

namespace SiNan.SDK;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSiNanClients(this IServiceCollection services, Action<SiNanClientOptions> configure)
    {
        services.Configure(configure);

        services.AddHttpClient("SiNan", (provider, client) =>
        {
            var options = provider.GetRequiredService<IOptions<SiNanClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = options.Timeout;
        });

        services.AddTransient<ISiNanRegistryClient, SiNanRegistryClient>();
        services.AddTransient<ISiNanConfigClient, SiNanConfigClient>();

        return services;
    }
}
