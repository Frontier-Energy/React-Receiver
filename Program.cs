using MediatR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using React_Receiver.Application.Auth;
using React_Receiver.Application.FormSchemas;
using React_Receiver.Application.Inspections;
using React_Receiver.Application.TenantConfig;
using React_Receiver.Application.Translations;
using React_Receiver.Application.Users;
using React_Receiver.Infrastructure.FormSchemas;
using React_Receiver.Infrastructure.Inspections;
using React_Receiver.Infrastructure.TenantConfig;
using React_Receiver.Infrastructure.Translations;
using React_Receiver.Infrastructure.Users;
using React_Receiver.Mediation.Behaviors;
using React_Receiver.Mediation.Exceptions;
using React_Receiver.Mediation.Transactions;
using React_Receiver.Middleware;
using React_Receiver.Observability;
using React_Receiver.Services;
using React_Receiver.Validation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddScoped<RequestValidationFilter>();
builder.Services.AddControllers(options =>
    {
        options.Filters.AddService<RequestValidationFilter>();
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "React-Receiver API", Version = "v1" });
});
builder.Services
    .AddOptions<BlobStorageOptions>()
    .Bind(builder.Configuration.GetSection("BlobStorage"))
    .ValidateOnStart();
builder.Services
    .AddOptions<QueueStorageOptions>()
    .Bind(builder.Configuration.GetSection("QueueStorage"))
    .ValidateOnStart();
builder.Services
    .AddOptions<TableStorageOptions>()
    .Bind(builder.Configuration.GetSection("TableStorage"))
    .ValidateOnStart();
builder.Services
    .AddOptions<BootstrapDataOptions>()
    .Bind(builder.Configuration.GetSection("BootstrapData"));
builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<BlobStorageOptions>, BlobStorageOptionsValidator>();
builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<QueueStorageOptions>, QueueStorageOptionsValidator>();
builder.Services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<TableStorageOptions>, TableStorageOptionsValidator>();
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BlobStorageOptions>>().Value;
    return new Azure.Storage.Blobs.BlobServiceClient(options.ConnectionString);
});
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QueueStorageOptions>>().Value;
    return new Azure.Storage.Queues.QueueServiceClient(options.ConnectionString);
});
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TableStorageOptions>>().Value;
    return new Azure.Data.Tables.TableServiceClient(options.ConnectionString);
});
builder.Services.AddHealthChecks()
    .AddCheck<StorageConfigurationHealthCheck>(
        "storage-config",
        tags: ["startup", "ready"])
    .AddCheck<BlobStorageHealthCheck>(
        "blob-storage",
        tags: ["startup", "ready"])
    .AddCheck<TableStorageHealthCheck>(
        "table-storage",
        tags: ["startup", "ready"]);
builder.Services.AddSingleton<IBootstrapDataProvider, FileBootstrapDataProvider>();
builder.Services.AddSingleton<React_Receiver.Handlers.IReceiveInspectionRequestParser, React_Receiver.Handlers.ReceiveInspectionRequestParser>();
builder.Services.AddSingleton<IUserRepository, AzureTableUserRepository>();
builder.Services.AddSingleton<IInspectionRepository, AzureInspectionRepository>();
builder.Services.AddSingleton<IFormSchemaRepository, AzureFormSchemaRepository>();
builder.Services.AddSingleton<ITranslationRepository, AzureTableTranslationRepository>();
builder.Services.AddSingleton<ITenantConfigRepository, AzureTableTenantConfigRepository>();
builder.Services.AddSingleton<IAuthApplicationService, AuthApplicationService>();
builder.Services.AddSingleton<IInspectionApplicationService, InspectionApplicationService>();
builder.Services.AddSingleton<IUserApplicationService, UserApplicationService>();
builder.Services.AddSingleton<IFormSchemaApplicationService, FormSchemaApplicationService>();
builder.Services.AddSingleton<ITranslationApplicationService, TranslationApplicationService>();
builder.Services.AddSingleton<ITenantConfigApplicationService, TenantConfigApplicationService>();
builder.Services.AddSingleton<IRequestTransaction, NoOpRequestTransaction>();
builder.Services.AddSingleton<IAuditEventLogger, AuditEventLogger>();
builder.Services.AddSingleton<IStorageOperationObserver, StorageOperationObserver>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));
builder.Services.AddRequestValidation();
builder.Services.AddHostedService<StartupHealthCheckHostedService>();
builder.Services.AddHostedService<StorageInfrastructureHostedService>();
builder.Services.AddHostedService<BootstrapDataHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "React-Receiver API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseMiddleware<RequestObservabilityMiddleware>();
app.UseExceptionHandler();
app.UseHttpsRedirection();

app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready")
});
app.MapHealthChecks("/health/startup", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("startup")
});

app.Run();
