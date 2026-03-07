using Microsoft.Extensions.DependencyInjection;
using React_Receiver.Infrastructure.Inspections;

namespace React_Receiver.Application.Inspections;

public static class InspectionServiceCollectionExtensions
{
    public static IServiceCollection AddInspectionFeature(this IServiceCollection services)
    {
        services.AddScoped<IInspectionRepository, AzureInspectionRepository>();
        services.AddScoped<IInspectionApplicationService, InspectionApplicationService>();
        services.AddScoped<React_Receiver.Handlers.IReceiveInspectionRequestParser, React_Receiver.Handlers.ReceiveInspectionRequestParser>();
        return services;
    }
}
