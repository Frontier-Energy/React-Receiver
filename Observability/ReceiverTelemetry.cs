using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace React_Receiver.Observability;

public static class ReceiverTelemetry
{
    public const string CorrelationHeaderName = "X-Correlation-ID";
    public const string MeterName = "React_Receiver.Observability";
    public const string ActivitySourceName = "React_Receiver.Activities";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static readonly Histogram<double> HttpRequestDurationMs =
        Meter.CreateHistogram<double>("react_receiver.http.request.duration", unit: "ms");

    public static readonly Counter<long> HttpRequests =
        Meter.CreateCounter<long>("react_receiver.http.request.count");

    public static readonly Histogram<double> MediatorRequestDurationMs =
        Meter.CreateHistogram<double>("react_receiver.mediatr.request.duration", unit: "ms");

    public static readonly Counter<long> AuditEvents =
        Meter.CreateCounter<long>("react_receiver.audit.event.count");

    public static readonly Histogram<double> StorageOperationDurationMs =
        Meter.CreateHistogram<double>("react_receiver.storage.operation.duration", unit: "ms");

    public static readonly Counter<long> StorageOperationFailures =
        Meter.CreateCounter<long>("react_receiver.storage.operation.failures");
}
