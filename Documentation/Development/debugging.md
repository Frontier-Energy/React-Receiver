# Debugging

This document covers the most common ways to debug React-Receiver locally and in shared environments.

## Start With The Request Path

For most feature issues, trace the request through this path:

`Controller -> MediatR handler -> Application Service -> Repository -> Azure Storage`

## Useful Endpoints During Debugging

- `GET /health/live`
- `GET /health/ready`
- `GET /health/startup`
- `GET /swagger`

## Correlation And Observability

`RequestObservabilityMiddleware` assigns or propagates `X-Correlation-ID`.

When debugging a request:

1. capture the correlation ID
2. look for logs produced by the controller, MediatR pipeline, and hosted services
3. correlate storage-operation logs if the failure is downstream

## Common Debugging Scenarios

### Startup fails immediately

Check:

- `Program.cs`
- `Services/ServiceCollectionExtensions.cs`
- option-validation errors
- startup health-check failures

### Endpoint returns `428` or `412`

Check:

- whether the client sent `If-Match`
- whether the client used the latest `ETag`
- `Application/Concurrency/OptimisticConcurrency.cs`
- `Mediation/Exceptions/ApiExceptionHandler.cs`

### Inspection accepted but not fully processed

Check:

- `InspectionIngestOutboxEntity` state
- queue availability
- retry hosted service logs
- `LastError`, `RetryCount`, `NextAttemptAtUtc`, `LockedUntilUtc`

### Translation, schema, or tenant-config content is missing

Check:

- whether storage-backed data exists
- whether seed-store fallback is in effect
- whether bootstrap data was imported

## Hosted Services To Remember

Several important behaviors happen outside the request thread:

- storage infrastructure creation
- startup dependency validation
- bootstrap-data import
- inspection ingest retry
