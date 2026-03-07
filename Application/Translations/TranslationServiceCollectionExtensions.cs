using Microsoft.Extensions.DependencyInjection;
using React_Receiver.Infrastructure.Translations;

namespace React_Receiver.Application.Translations;

public static class TranslationServiceCollectionExtensions
{
    public static IServiceCollection AddTranslationFeature(this IServiceCollection services)
    {
        services.AddSingleton<ITranslationSeedStore, TranslationSeedStore>();
        services.AddScoped<ITranslationRepository, AzureTableTranslationRepository>();
        services.AddScoped<ITranslationApplicationService, TranslationApplicationService>();
        return services;
    }
}
