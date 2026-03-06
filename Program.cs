var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "React-Receiver API", Version = "v1" });
});
builder.Services.Configure<React_Receiver.Services.BlobStorageOptions>(
    builder.Configuration.GetSection("BlobStorage"));
builder.Services.Configure<React_Receiver.Services.QueueStorageOptions>(
    builder.Configuration.GetSection("QueueStorage"));
builder.Services.Configure<React_Receiver.Services.TableStorageOptions>(
    builder.Configuration.GetSection("TableStorage"));
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<React_Receiver.Services.BlobStorageOptions>>().Value;
    return new Azure.Storage.Blobs.BlobServiceClient(options.ConnectionString);
});
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<React_Receiver.Services.QueueStorageOptions>>().Value;
    return new Azure.Storage.Queues.QueueServiceClient(options.ConnectionString);
});
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<React_Receiver.Services.TableStorageOptions>>().Value;
    return new Azure.Data.Tables.TableServiceClient(options.ConnectionString);
});
builder.Services.AddSingleton<React_Receiver.Handlers.IInspectionRequestHandler, React_Receiver.Handlers.InspectionRequestHandler>();
builder.Services.AddSingleton<React_Receiver.Handlers.ILoginRequestHandler, React_Receiver.Handlers.LoginRequestHandler>();
builder.Services.AddSingleton<React_Receiver.Handlers.IReceiveInspectionRequestParser, React_Receiver.Handlers.ReceiveInspectionRequestParser>();
builder.Services.AddSingleton<React_Receiver.Handlers.IRegisterRequestHandler, React_Receiver.Handlers.RegisterRequestHandler>();
builder.Services.AddSingleton<React_Receiver.Handlers.ITenantConfigHandler, React_Receiver.Handlers.TenantConfigHandler>();
builder.Services.AddSingleton<React_Receiver.Services.IInspectionQueryService, React_Receiver.Services.InspectionQueryService>();
builder.Services.AddSingleton<React_Receiver.Services.IUserQueryService, React_Receiver.Services.UserQueryService>();
builder.Services.AddSingleton<React_Receiver.Services.IFormSchemaService, React_Receiver.Services.FormSchemaService>();
builder.Services.AddSingleton<React_Receiver.Services.ITranslationService, React_Receiver.Services.TranslationService>();

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

app.Run();
