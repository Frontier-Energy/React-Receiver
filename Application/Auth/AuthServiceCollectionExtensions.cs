using Microsoft.Extensions.DependencyInjection;

namespace React_Receiver.Application.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddAuthFeature(this IServiceCollection services)
    {
        services.AddScoped<IAuthApplicationService, AuthApplicationService>();
        return services;
    }
}
