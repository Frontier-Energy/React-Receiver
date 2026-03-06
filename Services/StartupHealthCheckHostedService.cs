using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace React_Receiver.Services;

public sealed class StartupHealthCheckHostedService : IHostedService
{
    private readonly HealthCheckService _healthCheckService;
    private readonly ILogger<StartupHealthCheckHostedService> _logger;

    public StartupHealthCheckHostedService(
        HealthCheckService healthCheckService,
        ILogger<StartupHealthCheckHostedService> logger)
    {
        _healthCheckService = healthCheckService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(
            registration => registration.Tags.Contains("startup"),
            cancellationToken);

        if (report.Status == HealthStatus.Healthy)
        {
            return;
        }

        var failures = report.Entries
            .Where(static entry => entry.Value.Status != HealthStatus.Healthy)
            .Select(static entry => $"{entry.Key}: {entry.Value.Description ?? "unhealthy"}")
            .ToArray();

        _logger.LogCritical("Startup health checks failed: {Failures}", string.Join("; ", failures));
        throw new InvalidOperationException($"Startup health checks failed: {string.Join("; ", failures)}");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
