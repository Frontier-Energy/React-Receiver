using MediatR;
using Microsoft.Extensions.DependencyInjection;
using React_Receiver.Application.Inspections;
using React_Receiver.Infrastructure.Inspections;

namespace React_Receiver.Services;

public sealed class InspectionIngestRetryHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 25;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<InspectionIngestRetryHostedService> _logger;

    public InspectionIngestRetryHostedService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<InspectionIngestRetryHostedService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IInspectionRepository>();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var pendingSessions = await repository.GetPendingSessionIdsAsync(BatchSize, stoppingToken);

                foreach (var sessionId in pendingSessions)
                {
                    await sender.Send(new ProcessInspectionIngestCommand(sessionId), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Inspection ingest retry loop failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
