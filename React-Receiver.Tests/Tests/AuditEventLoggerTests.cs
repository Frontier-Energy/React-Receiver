using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using React_Receiver.Observability;
using Xunit;

namespace React_Receiver.Tests;

public sealed class AuditEventLoggerTests
{
    [Fact]
    public void Log_OnlyPromotesLowCardinalityProperties_ToScopesAndMetrics()
    {
        var testLogger = new TestLogger<AuditEventLogger>();
        using var listener = new MeterListener();
        var capturedMeasurements = new List<(long Value, KeyValuePair<string, object?>[] Tags)>();

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == ReceiverTelemetry.MeterName &&
                instrument.Name == "react_receiver.audit.event.count")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((_, measurement, tags, _) =>
        {
            capturedMeasurements.Add((measurement, tags.ToArray()));
        });
        listener.Start();

        var sut = new AuditEventLogger(testLogger);

        sut.Log(
            "form_schema.mutate",
            new Dictionary<string, object?>
            {
                ["result"] = "success",
                ["created"] = true,
                ["language"] = "en-US",
                ["formType"] = "electrical-sf",
                ["tenantId"] = "tenant-42",
                ["sessionId"] = "session-99",
                ["userId"] = "user-88",
                ["email"] = "a***@example.com",
                ["fileCount"] = 3
            });

        var scope = Assert.Single(testLogger.Scopes);
        Assert.Equal("success", scope["result"]);
        Assert.Equal(true, scope["created"]);
        Assert.Equal("en-US", scope["language"]);
        Assert.False(scope.ContainsKey("formType"));
        Assert.False(scope.ContainsKey("tenantId"));
        Assert.False(scope.ContainsKey("sessionId"));
        Assert.False(scope.ContainsKey("userId"));
        Assert.False(scope.ContainsKey("email"));
        Assert.False(scope.ContainsKey("fileCount"));

        var measurement = Assert.Single(capturedMeasurements);
        Assert.Equal(1L, measurement.Value);
        Assert.Contains(measurement.Tags, tag => tag.Key == "audit.event" && Equals(tag.Value, "form_schema.mutate"));
        Assert.Contains(measurement.Tags, tag => tag.Key == "result" && Equals(tag.Value, "success"));
        Assert.Contains(measurement.Tags, tag => tag.Key == "created" && Equals(tag.Value, true));
        Assert.Contains(measurement.Tags, tag => tag.Key == "language" && Equals(tag.Value, "en-US"));
        Assert.DoesNotContain(measurement.Tags, tag => tag.Key == "formType");
        Assert.DoesNotContain(measurement.Tags, tag => tag.Key == "tenantId");
        Assert.DoesNotContain(measurement.Tags, tag => tag.Key == "sessionId");
        Assert.DoesNotContain(measurement.Tags, tag => tag.Key == "userId");
        Assert.DoesNotContain(measurement.Tags, tag => tag.Key == "email");
        Assert.DoesNotContain(measurement.Tags, tag => tag.Key == "fileCount");
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<IReadOnlyDictionary<string, object?>> Scopes { get; } = [];

        IDisposable ILogger.BeginScope<TState>(TState state)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> properties)
            {
                Scopes.Add(new Dictionary<string, object?>(properties, StringComparer.Ordinal));
            }

            return NullScope.Instance;
        }

        bool ILogger.IsEnabled(LogLevel logLevel) => true;

        void ILogger.Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
