# Observability

## Azure Application Insights

React-Receiver exports telemetry through Azure Monitor OpenTelemetry.

Provide the Application Insights connection string through one of these configuration keys:

- `AzureMonitor__ConnectionString`
- `APPLICATIONINSIGHTS_CONNECTION_STRING`

Recommended:

- prefer environment variables or deployment-time secret injection
- do not store production connection strings in source-controlled appsettings files

## Collected telemetry

The app emits:

- ASP.NET Core request telemetry
- Azure SDK dependency telemetry
- `ILogger` logs
- custom metrics from `React_Receiver.Observability`
- MediatR trace spans from `React_Receiver.Activities`

## Dashboard starting points

In Application Insights, start with:

- Requests: request rate, failure rate, and duration
- Dependencies: Azure Storage latency and failures
- Exceptions: unhandled failures
- Traces: correlation and audit flow
- Custom Metrics: `react_receiver.*`

## Useful queries

Requests by route:

```kusto
requests
| summarize Count = count(), AvgDurationMs = avg(duration / 1ms) by name, success
| order by Count desc
```

Custom metrics from the app meter:

```kusto
customMetrics
| where name startswith "react_receiver."
| summarize AvgValue = avg(value), SumValue = sum(value) by name
| order by name asc
```

MediatR spans:

```kusto
dependencies
| where type == "InProc" or name startswith "MediatR "
| project timestamp, operation_Name, name, success, duration, target
| order by timestamp desc
```
