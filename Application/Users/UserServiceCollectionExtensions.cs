using Microsoft.Extensions.DependencyInjection;
using React_Receiver.Infrastructure.Users;

namespace React_Receiver.Application.Users;

public static class UserServiceCollectionExtensions
{
    public static IServiceCollection AddUserFeature(this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, AzureTableUserRepository>();
        services.AddScoped<IUserApplicationService, UserApplicationService>();
        return services;
    }
}
