using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using React_Receiver.Application.Inspections;
using React_Receiver.Infrastructure.Inspections;

namespace React_Receiver.Services;

public sealed class InspectionIngestRetryHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<InspectionIngestRetryHostedService> _logger;
    private readonly InspectionIngestRetryOptions _options;

    public InspectionIngestRetryHostedService(
        IServiceScopeFactory serviceScopeFactory,
        ILogger<InspectionIngestRetryHostedService> logger,
        IOptions<InspectionIngestRetryOptions> options)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var shouldDelay = true;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var pendingSessions = await GetPendingSessionIdsAsync(stoppingToken);
                    if (pendingSessions.Count == 0)
                    {
                        break;
                    }

                    await ProcessSessionsAsync(pendingSessions, stoppingToken);

                    if (pendingSessions.Count < _options.BatchSize)
                    {
                        break;
                    }

                    shouldDelay = false;
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

            if (!shouldDelay)
            {
                continue;
            }

            try
            {
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task<IReadOnlyCollection<string>> GetPendingSessionIdsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IInspectionRepository>();
        return await repository.GetPendingSessionIdsAsync(_options.BatchSize, cancellationToken);
    }

    private Task ProcessSessionsAsync(
        IReadOnlyCollection<string> pendingSessions,
        CancellationToken cancellationToken)
    {
        return Parallel.ForEachAsync(
            pendingSessions,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.MaxConcurrentSessions,
                CancellationToken = cancellationToken
            },
            async (sessionId, ct) => await ProcessSessionAsync(sessionId, ct));
    }

    private async Task ProcessSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var result = await sender.Send(new ProcessInspectionIngestCommand(sessionId), cancellationToken);
            if (result.TerminalFailure)
            {
                _logger.LogError(
                    "Inspection ingest session {SessionId} entered terminal state {Status} after {RetryCount} retries: {LastError}",
                    sessionId,
                    result.Status,
                    result.RetryCount,
                    result.LastError);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Inspection ingest retry failed for session {SessionId}", sessionId);
        }
    }
}
