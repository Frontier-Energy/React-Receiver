# React-Receiver Architecture

## Purpose

React-Receiver is an ASP.NET Core Web API that accepts inspection submissions and exposes supporting configuration data used by a client application. The service is organized around a small set of features:

- inspection ingestion and retrieval
- authentication and user lookup
- tenant configuration
- form schema catalog and schema content
- translations

At a high level, the API accepts HTTP requests, routes them through MediatR request handlers, and persists state in Azure Blob Storage, Azure Table Storage, and Azure Queue Storage.

## Architectural Style

The codebase uses a pragmatic layered design rather than strict clean architecture.

- `Controllers/` defines the HTTP surface.
- `Application/` contains request handlers, application services, and feature wiring.
- `Infrastructure/` contains Azure-backed repositories.
- `Models/` contains request, response, and storage entity models.
- `Services/`, `Middleware/`, `Mediation/`, and `Observability/` contain cross-cutting runtime concerns.

The controller layer is intentionally thin. Most endpoints send a MediatR command or query, and handlers delegate to a feature-specific application service or repository.

## Runtime Composition

`Program.cs` composes the application in five broad blocks:

1. API services: controllers, request validation, Swagger, exception handling, and problem details.
2. Storage services: Azure SDK clients, storage option binding, health checks, and shared infrastructure services.
3. Feature services: auth, users, inspections, form schemas, translations, and tenant config.
4. MediatR: handlers plus pipeline behaviors for logging, auditing, exception capture, and request transactions.
5. Hosted services: storage bootstrap, startup readiness, seed import, and inspection retry processing.

This keeps startup simple: the top-level program mostly wires modules together, while each feature owns its own registrations.

## Request Flow

The common request path is:

`HTTP -> Controller -> MediatR command/query -> Handler -> Application Service -> Repository -> Azure Storage`

Cross-cutting behaviors run around handler execution:

- request validation is enforced before controller logic
- request and mediator timing is recorded for telemetry
- audit events are emitted for selected commands
- exceptions are translated into RFC 7807 problem responses
- transactional behavior exists as an extension point, although the current implementation is a no-op transaction

## Core Storage Model

The service uses Azure storage primitives directly instead of a relational database.

### Blob Storage

- primary inspection payloads are stored as JSON blobs in the configured raw container
- uploaded inspection files are stored in a dedicated `files` container
- form schema content may also be backed by blob storage through the schema repository

### Table Storage

- users
- current-user profile
- inspection file metadata
- inspection ingest outbox
- tenant configuration
- form schema catalog
- form schemas
- translations

### Queue Storage

- a processor queue receives inspection session messages after staging succeeds

This design favors simple Azure-native building blocks over distributed transactions. Where operations span multiple stores, the code uses an outbox-style workflow for durability and retry.

## Inspection Ingest Architecture

Inspection ingest is the most important workflow in the service.

### Receive path

`POST /inspections` accepts `multipart/form-data` with:

- a JSON payload field
- optional uploaded files

The request is parsed by `ReceiveInspectionRequestParser`, then handled by `ReceiveInspectionCommandHandler`, which calls `InspectionApplicationService`.

### Staging and outbox

`AzureInspectionRepository.PrepareAsync` does not try to complete the entire workflow atomically. Instead it:

1. normalizes the request and assigns a session id if one was not supplied
2. builds a manifest for uploaded files
3. creates or reloads an `InspectionIngestOutboxEntity`
4. stages the payload JSON to blob storage
5. stages uploaded files to the `files` blob container
6. updates outbox status flags after each successful stage

If staging fails, the repository performs compensating deletes and marks the outbox record as compensated with the last error.

### Finalization

Finalization is separated from staging. `ProcessInspectionIngestCommandHandler` calls `ProcessPendingAsync`, which:

1. acquires a lightweight lease on the outbox row
2. writes inspection file metadata to table storage
3. sends a queue message containing the `sessionId`
4. marks the outbox record as completed

Because these steps are resumable, the API can acknowledge receipt before every downstream action finishes.

### Retry model

`InspectionApplicationService` attempts finalization immediately after staging. If that inline attempt fails, the API still returns success and relies on `InspectionIngestRetryHostedService` to continue processing pending sessions.

Retry behavior is driven by fields on `InspectionIngestOutboxEntity`:

- `PayloadStaged`
- `FilesStaged`
- `MetadataWritten`
- `QueueMessageSent`
- `Completed`
- `Processing`
- `RetryCount`
- `NextAttemptAtUtc`
- `LockedUntilUtc`
- `LastError`

This is effectively an application-managed outbox with optimistic lease acquisition and exponential backoff.

## Feature Boundaries

### Auth and users

Auth is lightweight and repository-driven. Login is essentially an email lookup; registration creates a user if one does not already exist. The current-user endpoint also supports a default seeded profile when no stored profile exists.

### Tenant config, translations, and form schemas

These three features follow a similar pattern:

- controller sends query or upsert command
- application service chooses between repository-backed data and in-memory seed data
- repository persistence uses Azure Table Storage, with blob storage used where larger schema content is needed

These endpoints support optimistic concurrency through `ETag` headers. If a client omits `If-Match` when required, the API returns `428 Precondition Required`; if the ETag does not match, it returns `412 Precondition Failed`.

## Startup and Background Work

Hosted services handle infrastructure concerns that do not belong in request handlers.

- `StorageInfrastructureHostedService` ensures required blob containers, queue, and tables exist on startup.
- `StartupHealthCheckHostedService` runs startup-tagged health checks and aborts startup if critical dependencies are not healthy.
- `BootstrapDataHostedService` imports seed data for tenant config, translations, and form schemas when enabled.
- `InspectionIngestRetryHostedService` polls for incomplete inspection ingests and retries finalization.

This keeps the request path focused on user-facing work while still allowing the service to self-initialize in lower environments.

## Observability

Observability is built into the pipeline rather than added ad hoc.

- `RequestObservabilityMiddleware` assigns or propagates `X-Correlation-ID`
- request duration and request count metrics are emitted
- MediatR request duration is recorded
- audit events are emitted for login, registration, inspection ingest, and content mutation flows
- storage operations are wrapped by `IStorageOperationObserver` so latency and failure metrics can be collected consistently

The practical effect is that a single request can be traced across HTTP logs, MediatR execution, audit events, and storage calls.

## Health and Configuration

Health endpoints are exposed at:

- `/health/live`
- `/health/ready`
- `/health/startup`

Configuration is bound from `BlobStorage`, `QueueStorage`, `TableStorage`, and `BootstrapData` sections. The intended architecture is environment-provided configuration for connection strings and storage names. Sensitive values should be supplied through deployment configuration or a secret store rather than treated as source-controlled defaults.

## Design Tradeoffs

This architecture makes a few explicit tradeoffs:

- It favors Azure platform primitives over a relational database and message broker stack.
- It accepts eventual consistency for inspection ingest in exchange for resilience across blob, table, and queue writes.
- It keeps controllers thin and business logic close to handlers and application services.
- It uses MediatR behaviors for cross-cutting concerns, which keeps features consistent but adds an indirect execution path.

## Summary

React-Receiver is a small Azure-native integration API. Its most significant architectural choice is the inspection ingest outbox flow, which turns a multi-store write into a staged, retryable process. The rest of the system follows the same general shape: thin HTTP endpoints, MediatR orchestration, feature-local application services, and Azure-backed repositories with observability and health checks built into the runtime.
