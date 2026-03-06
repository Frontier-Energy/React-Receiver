using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using React_Receiver.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
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
builder.Services.AddSingleton<React_Receiver.Handlers.IInspectionRequestHandler, React_Receiver.Handlers.InspectionRequestHandler>();
builder.Services.AddSingleton<React_Receiver.Handlers.ILoginRequestHandler, React_Receiver.Handlers.LoginRequestHandler>();
builder.Services.AddSingleton<React_Receiver.Handlers.IReceiveInspectionRequestParser, React_Receiver.Handlers.ReceiveInspectionRequestParser>();
builder.Services.AddSingleton<React_Receiver.Handlers.IRegisterRequestHandler, React_Receiver.Handlers.RegisterRequestHandler>();
builder.Services.AddSingleton<React_Receiver.Handlers.ITenantConfigHandler, React_Receiver.Handlers.TenantConfigHandler>();
builder.Services.AddSingleton<IInspectionQueryService, InspectionQueryService>();
builder.Services.AddSingleton<IUserQueryService, UserQueryService>();
builder.Services.AddSingleton<IFormSchemaService, FormSchemaService>();
builder.Services.AddSingleton<ITranslationService, TranslationService>();
builder.Services.AddHostedService<StartupHealthCheckHostedService>();
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
