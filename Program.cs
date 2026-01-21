var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddOpenApi();
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
builder.Services.AddSingleton<React_Receiver.Handlers.InspectionRequestHandler>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
