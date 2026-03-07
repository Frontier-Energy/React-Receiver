using Microsoft.Extensions.DependencyInjection;
using React_Receiver.Infrastructure.TenantConfig;

namespace React_Receiver.Application.TenantConfig;

public static class TenantConfigServiceCollectionExtensions
{
    public static IServiceCollection AddTenantConfigFeature(this IServiceCollection services)
    {
        services.AddSingleton<ITenantConfigSeedStore, TenantConfigSeedStore>();
        services.AddScoped<ITenantConfigRepository, AzureTableTenantConfigRepository>();
        services.AddScoped<ITenantConfigApplicationService, TenantConfigApplicationService>();
        return services;
    }
}
