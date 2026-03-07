using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using React_Receiver.Application.Auth;
using React_Receiver.Application.FormSchemas;
using React_Receiver.Application.Inspections;
using React_Receiver.Application.TenantConfig;
using React_Receiver.Application.Translations;
using React_Receiver.Application.Users;
using React_Receiver.Middleware;
using React_Receiver.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiServices()
    .AddStorageServices(builder.Configuration)
    .AddAuthFeature()
    .AddUserFeature()
    .AddInspectionFeature()
    .AddFormSchemaFeature()
    .AddTranslationFeature()
    .AddTenantConfigFeature()
    .AddMediatorServices()
    .AddHostedServices();

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
