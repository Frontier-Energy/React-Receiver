using System.Diagnostics;
using React_Receiver.Observability;

namespace React_Receiver.Middleware;

public sealed class RequestObservabilityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestObservabilityMiddleware> _logger;

    public RequestObservabilityMiddleware(
        RequestDelegate next,
        ILogger<RequestObservabilityMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[ReceiverTelemetry.CorrelationHeaderName] = correlationId;

        Activity.Current?.SetTag("correlation.id", correlationId);
        Activity.Current?.AddBaggage("correlation.id", correlationId);

        if (!RequestTelemetryFilter.ShouldCollect(context.Request.Path))
        {
            await _next(context);
            return;
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = Activity.Current?.TraceId.ToString(),
            ["RequestMethod"] = context.Request.Method,
            ["RequestPath"] = context.Request.Path.Value
        });

        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            _logger.LogInformation(
                "HTTP request started for {RequestMethod} {RequestPath}",
                context.Request.Method,
                context.Request.Path);

            await _next(context);
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
            var endpoint = context.GetEndpoint()?.DisplayName ?? "unknown";
            var statusCode = context.Response.StatusCode;
            var outcome = statusCode >= 500 ? "server_error" :
                statusCode >= 400 ? "client_error" : "success";

            var tags = new TagList
            {
                { "http.method", context.Request.Method },
                { "http.path", context.Request.Path.Value },
                { "http.route", endpoint },
                { "http.status_code", statusCode },
                { "outcome", outcome }
            };

            ReceiverTelemetry.HttpRequestDurationMs.Record(elapsedMs, tags);
            ReceiverTelemetry.HttpRequests.Add(1, tags);

            _logger.LogInformation(
                "HTTP request completed for {RequestMethod} {RequestPath} with {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                statusCode,
                elapsedMs);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        var headerValue = context.Request.Headers[ReceiverTelemetry.CorrelationHeaderName]
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue;
        }

        var traceId = Activity.Current?.TraceId.ToString();
        return string.IsNullOrWhiteSpace(traceId)
            ? context.TraceIdentifier
            : traceId;
    }
}
