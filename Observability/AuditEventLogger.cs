using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace React_Receiver.Observability;

public interface IAuditEventLogger
{
    void Log(string eventName, IReadOnlyDictionary<string, object?> properties);
}

public sealed class AuditEventLogger : IAuditEventLogger
{
    private static readonly HashSet<string> AllowedTelemetryProperties = new(StringComparer.Ordinal)
    {
        "created",
        "errorType",
        "language",
        "result"
    };

    private readonly ILogger<AuditEventLogger> _logger;

    public AuditEventLogger(ILogger<AuditEventLogger> logger)
    {
        _logger = logger;
    }

    public void Log(string eventName, IReadOnlyDictionary<string, object?> properties)
    {
        var telemetryProperties = CreateTelemetryProperties(properties);

        using var scope = _logger.BeginScope(telemetryProperties);
        _logger.LogInformation("Audit event {AuditEventName}", eventName);

        var tags = new TagList
        {
            { "audit.event", eventName }
        };

        foreach (var property in telemetryProperties)
        {
            if (property.Value is null)
            {
                continue;
            }

            tags.Add(property.Key, property.Value);
        }

        ReceiverTelemetry.AuditEvents.Add(1, tags);
    }

    private static Dictionary<string, object?> CreateTelemetryProperties(IReadOnlyDictionary<string, object?> properties)
    {
        var filtered = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var property in properties)
        {
            if (!AllowedTelemetryProperties.Contains(property.Key) || property.Value is null)
            {
                continue;
            }

            filtered[property.Key] = property.Value;
        }

        return filtered;
    }
}
