using System.Diagnostics;
using System.Diagnostics.Metrics;
using MediatR;
using React_Receiver.Observability;

namespace React_Receiver.Mediation.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var startedAt = Stopwatch.GetTimestamp();

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["MediatorRequest"] = requestName
        });

        try
        {
            _logger.LogInformation("Handling MediatR request {RequestName}", requestName);

            var response = await next();
            var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            ReceiverTelemetry.MediatorRequestDurationMs.Record(
                elapsedMs,
                new TagList
                {
                    { "mediatr.request", requestName },
                    { "outcome", "success" }
                });
            _logger.LogInformation(
                "Handled MediatR request {RequestName} in {ElapsedMs}ms",
                requestName,
                elapsedMs);
            return response;
        }
        catch
        {
            var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            ReceiverTelemetry.MediatorRequestDurationMs.Record(
                elapsedMs,
                new TagList
                {
                    { "mediatr.request", requestName },
                    { "outcome", "failure" }
                });
            throw;
        }
    }
}
