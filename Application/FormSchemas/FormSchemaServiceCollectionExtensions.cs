using Microsoft.Extensions.DependencyInjection;
using React_Receiver.Infrastructure.FormSchemas;

namespace React_Receiver.Application.FormSchemas;

public static class FormSchemaServiceCollectionExtensions
{
    public static IServiceCollection AddFormSchemaFeature(this IServiceCollection services)
    {
        services.AddSingleton<IFormSchemaSeedStore, FormSchemaSeedStore>();
        services.AddScoped<IFormSchemaRepository, AzureFormSchemaRepository>();
        services.AddScoped<IFormSchemaApplicationService, FormSchemaApplicationService>();
        return services;
    }
}
