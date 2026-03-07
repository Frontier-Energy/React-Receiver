using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace React_Receiver.Observability;

public interface IAuditEventLogger
{
    void Log(string eventName, IReadOnlyDictionary<string, object?> properties);
}

public sealed class AuditEventLogger : IAuditEventLogger
{
    private readonly ILogger<AuditEventLogger> _logger;

    public AuditEventLogger(ILogger<AuditEventLogger> logger)
    {
        _logger = logger;
    }

    public void Log(string eventName, IReadOnlyDictionary<string, object?> properties)
    {
        using var scope = _logger.BeginScope(properties);
        _logger.LogInformation("Audit event {AuditEventName}", eventName);

        var tags = new TagList
        {
            { "audit.event", eventName }
        };

        foreach (var property in properties)
        {
            if (property.Value is null)
            {
                continue;
            }

            tags.Add(property.Key, property.Value);
        }

        ReceiverTelemetry.AuditEvents.Add(1, tags);
    }
}
