using Azure.Monitor.OpenTelemetry.AspNetCore;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using React_Receiver.Mediation.Behaviors;
using React_Receiver.Mediation.Exceptions;
using React_Receiver.Mediation.Transactions;
using React_Receiver.Observability;
using React_Receiver.Validation;

namespace React_Receiver.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddScoped<RequestValidationFilter>();
        services.AddControllers(options =>
            {
                options.Filters.AddService<RequestValidationFilter>();
            })
            .ConfigureApiBehaviorOptions(options =>
            {
                options.SuppressModelStateInvalidFilter = true;
            });
        services.AddEndpointsApiExplorer();
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new() { Title = "React-Receiver API", Version = "v1" });
        });
        services.AddRequestValidation();
        return services;
    }

    public static IServiceCollection AddObservabilityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.ConfigureOpenTelemetryMeterProvider((_, metrics) =>
        {
            metrics.AddMeter(ReceiverTelemetry.MeterName);
        });
        services.ConfigureOpenTelemetryTracerProvider((_, tracing) =>
        {
            tracing.AddSource(ReceiverTelemetry.ActivitySourceName);
        });
        services.AddOpenTelemetry().UseAzureMonitor(options =>
        {
            options.ConnectionString =
                configuration["AzureMonitor:ConnectionString"] ??
                configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        });

        return services;
    }

    public static IServiceCollection AddStorageServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<BlobStorageOptions>()
            .Bind(configuration.GetSection("BlobStorage"))
            .ValidateOnStart();
        services
            .AddOptions<QueueStorageOptions>()
            .Bind(configuration.GetSection("QueueStorage"))
            .ValidateOnStart();
        services
            .AddOptions<TableStorageOptions>()
            .Bind(configuration.GetSection("TableStorage"))
            .ValidateOnStart();
        services
            .AddOptions<BootstrapDataOptions>()
            .Bind(configuration.GetSection("BootstrapData"));

        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<BlobStorageOptions>, BlobStorageOptionsValidator>();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<QueueStorageOptions>, QueueStorageOptionsValidator>();
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<TableStorageOptions>, TableStorageOptionsValidator>();

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BlobStorageOptions>>().Value;
            return new Azure.Storage.Blobs.BlobServiceClient(options.ConnectionString);
        });
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QueueStorageOptions>>().Value;
            return new Azure.Storage.Queues.QueueServiceClient(options.ConnectionString);
        });
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TableStorageOptions>>().Value;
            return new Azure.Data.Tables.TableServiceClient(options.ConnectionString);
        });

        services.AddHealthChecks()
            .AddCheck<StorageConfigurationHealthCheck>(
                "storage-config",
                tags: ["startup", "ready"])
            .AddCheck<BlobStorageHealthCheck>(
                "blob-storage",
                tags: ["startup", "ready"])
            .AddCheck<TableStorageHealthCheck>(
                "table-storage",
                tags: ["startup", "ready"]);

        services.AddSingleton<IBootstrapDataProvider, FileBootstrapDataProvider>();
        services.AddSingleton<IAuditEventLogger, AuditEventLogger>();
        services.AddSingleton<IStorageOperationObserver, StorageOperationObserver>();
        services.AddScoped<IRequestTransaction, NoOpRequestTransaction>();
        return services;
    }

    public static IServiceCollection AddMediatorServices(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
        return services;
    }

    public static IServiceCollection AddHostedServices(this IServiceCollection services)
    {
        services.AddHostedService<StorageInfrastructureHostedService>();
        services.AddHostedService<StartupHealthCheckHostedService>();
        services.AddHostedService<BootstrapDataHostedService>();
        services.AddHostedService<InspectionIngestRetryHostedService>();
        return services;
    }
}
