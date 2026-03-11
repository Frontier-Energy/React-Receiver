using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Core;
using Azure.Identity;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using React_Receiver.Mediation.Behaviors;
using React_Receiver.Mediation.Exceptions;
using React_Receiver.Mediation.Transactions;
using React_Receiver.Observability;
using React_Receiver.Validation;

namespace React_Receiver.Services;

public static class ServiceCollectionExtensions
{
    internal static bool ShouldSkipHostedServicesForOpenApi(IConfiguration configuration)
    {
        return configuration.GetValue<bool>("GenerateOpenApi");
    }

    internal static string? ResolveAzureMonitorConnectionString(IConfiguration configuration)
    {
        var configuredValue = configuration["AzureMonitor:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(configuredValue))
        {
            return NormalizeAzureMonitorConnectionString(configuredValue);
        }

        var environmentValue = configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(environmentValue))
        {
            return NormalizeAzureMonitorConnectionString(environmentValue);
        }

        return null;
    }

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
            options.CustomOperationIds(apiDescription =>
            {
                var controller = apiDescription.ActionDescriptor.RouteValues.TryGetValue("controller", out var controllerName)
                    ? controllerName
                    : "Api";
                var action = apiDescription.ActionDescriptor.RouteValues.TryGetValue("action", out var actionName)
                    ? actionName
                    : apiDescription.HttpMethod ?? "Operation";
                return $"{controller}_{action}";
            });
        });
        services.AddRequestValidation();
        return services;
    }

    public static IServiceCollection AddObservabilityServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
        {
            options.RecordException = true;
            options.Filter = httpContext => RequestTelemetryFilter.ShouldCollect(httpContext.Request.Path);
        });
        var openTelemetryBuilder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: "react-receiver",
                serviceVersion: ResolveServiceVersion(configuration)));
        var azureMonitorConnectionString = ResolveAzureMonitorConnectionString(configuration);

        if (azureMonitorConnectionString is not null)
        {
            openTelemetryBuilder.UseAzureMonitor(options =>
            {
                options.ConnectionString = azureMonitorConnectionString;
            });
        }

        services.ConfigureOpenTelemetryMeterProvider((_, metrics) =>
        {
            metrics.AddMeter(ReceiverTelemetry.MeterName);
        });
        services.ConfigureOpenTelemetryTracerProvider((_, tracing) =>
        {
            tracing.AddSource(ReceiverTelemetry.ActivitySourceName);
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
        services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BlobStorageOptions>>().Value;
            return CreateBlobServiceClient(options, sp.GetRequiredService<TokenCredential>());
        });
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QueueStorageOptions>>().Value;
            return CreateQueueServiceClient(options, sp.GetRequiredService<TokenCredential>());
        });
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TableStorageOptions>>().Value;
            return CreateTableServiceClient(options, sp.GetRequiredService<TokenCredential>());
        });

        services.AddHealthChecks()
            .AddCheck<StorageConfigurationHealthCheck>(
                "storage-config",
                tags: ["startup", "ready"])
            .AddCheck<BlobStorageHealthCheck>(
                "blob-storage",
                tags: ["startup", "ready"])
            .AddCheck<QueueStorageHealthCheck>(
                "queue-storage",
                tags: ["startup", "ready"])
            .AddCheck<TableStorageHealthCheck>(
                "table-storage",
                tags: ["startup", "ready"]);

        services.AddSingleton<IBootstrapDataProvider, FileBootstrapDataProvider>();
        services.AddSingleton<IAuditEventLogger, AuditEventLogger>();
        services.AddSingleton<IStorageOperationObserver, StorageOperationObserver>();
        services.AddSingleton<IInspectionFileMalwareScanner, SignatureInspectionFileMalwareScanner>();
        services.AddSingleton<IInspectionFileSecurityInspector, InspectionFileSecurityInspector>();
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

    public static IServiceCollection AddHostedServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<InspectionIngestRetryOptions>()
            .BindConfiguration("InspectionIngestRetry")
            .PostConfigure(options =>
            {
                if (options.PollInterval <= TimeSpan.Zero)
                {
                    options.PollInterval = TimeSpan.FromSeconds(10);
                }

                if (options.BatchSize <= 0)
                {
                    options.BatchSize = 100;
                }

                if (options.MaxConcurrentSessions <= 0)
                {
                    options.MaxConcurrentSessions = 8;
                }

                if (options.PoisonThreshold <= 0)
                {
                    options.PoisonThreshold = 10;
                }
            });

        services.AddHostedService<StartupHealthCheckHostedService>();
        services.AddHostedService<BootstrapDataHostedService>();
        services.AddHostedService<InspectionIngestRetryHostedService>();
        return services;
    }

    private static string ResolveServiceVersion(IConfiguration configuration)
    {
        return configuration["APP_VERSION"] ??
            configuration["OTEL_SERVICE_VERSION"] ??
            typeof(Program).Assembly.GetName().Version?.ToString() ??
            "unknown";
    }

    private static Azure.Storage.Blobs.BlobServiceClient CreateBlobServiceClient(
        BlobStorageOptions options,
        TokenCredential credential)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new Azure.Storage.Blobs.BlobServiceClient(options.ConnectionString);
        }

        return new Azure.Storage.Blobs.BlobServiceClient(GetRequiredServiceUri(options.ServiceUri, "BlobStorage:ServiceUri"), credential);
    }

    private static Azure.Storage.Queues.QueueServiceClient CreateQueueServiceClient(
        QueueStorageOptions options,
        TokenCredential credential)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new Azure.Storage.Queues.QueueServiceClient(options.ConnectionString);
        }

        return new Azure.Storage.Queues.QueueServiceClient(GetRequiredServiceUri(options.ServiceUri, "QueueStorage:ServiceUri"), credential);
    }

    private static Azure.Data.Tables.TableServiceClient CreateTableServiceClient(
        TableStorageOptions options,
        TokenCredential credential)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return new Azure.Data.Tables.TableServiceClient(options.ConnectionString);
        }

        return new Azure.Data.Tables.TableServiceClient(GetRequiredServiceUri(options.ServiceUri, "TableStorage:ServiceUri"), credential);
    }

    private static Uri GetRequiredServiceUri(string? serviceUri, string settingName)
    {
        if (string.IsNullOrWhiteSpace(serviceUri))
        {
            throw new InvalidOperationException($"{settingName} is required when no connection string is configured.");
        }

        return new Uri(serviceUri, UriKind.Absolute);
    }

    private static string? NormalizeAzureMonitorConnectionString(string connectionString)
    {
        var normalized = connectionString.Trim();
        if (normalized.Length == 0 || normalized.StartsWith(';'))
        {
            return null;
        }

        return normalized;
    }
}
