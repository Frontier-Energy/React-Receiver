# Major Design Patterns In React-Receiver

This document explains the main design patterns used in this codebase in plain language. It is not a catalog of every class-level technique. It focuses on the patterns that meaningfully shape how requests flow, how data is stored, and how failures are handled.

## 1. Layered Architecture

### What it means

The code is split into layers with different jobs:

- `Controllers/` handles HTTP concerns.
- `Application/` coordinates use cases.
- `Infrastructure/` talks to Azure services.
- `Models/` holds request, response, and storage shapes.
- `Services/`, `Mediation/`, `Middleware/`, and `Observability/` handle shared runtime concerns.

### How it looks here

A typical request flows like this:

`HTTP -> Controller -> MediatR request -> Handler -> Application Service -> Repository -> Azure Storage`

Examples:

- `Controllers/InspectionsController.cs`
- `Application/Inspections/ReceiveInspectionCommandHandler.cs`
- `Application/Inspections/InspectionApplicationService.cs`
- `Infrastructure/Inspections/AzureInspectionRepository.cs`

### Why this pattern is used

It keeps web concerns, business coordination, and storage logic from getting mixed together. That makes the code easier to change without rewriting unrelated parts.

## 2. Mediator + CQRS-lite

### What it means

Instead of controllers calling services directly for everything, they send commands and queries through MediatR.

- A command changes state.
- A query reads state.

This is not full enterprise CQRS with separate read and write databases. It is a lightweight version used to keep request handling consistent.

### How it looks here

Examples:

- `ReceiveInspectionCommand` and `ReceiveInspectionCommandHandler`
- `ProcessInspectionIngestCommand` and `ProcessInspectionIngestCommandHandler`
- `GetInspectionQuery` and `GetInspectionQueryHandler`
- `GetTranslationsQuery` and `GetTranslationsQueryHandler`

Controllers stay thin because they mostly do `_sender.Send(...)`.

### Why this pattern is used

It gives each use case a clear entry point and makes it easy to add cross-cutting behavior around all requests.

## 3. Application Service Pattern

### What it means

Handlers do not usually contain the full business workflow themselves. They delegate to an application service that coordinates the work.

Think of the application service as the feature-level orchestrator.

### How it looks here

Examples:

- `InspectionApplicationService`
- `TranslationApplicationService`
- `TenantConfigApplicationService`
- `FormSchemaApplicationService`

`InspectionApplicationService` is a good example. It stages an inspection, then tries to trigger finalization immediately, and falls back to async retry if that second step fails.

### Why this pattern is used

It keeps handlers small and puts workflow logic in one place per feature instead of scattering it across controllers, handlers, and repositories.

## 4. Repository Pattern

### What it means

Repositories hide the details of Azure Table Storage, Blob Storage, and Queue Storage behind interfaces that describe the operations the application actually cares about.

### How it looks here

Examples:

- `IInspectionRepository` / `AzureInspectionRepository`
- `ITranslationRepository` / `AzureTableTranslationRepository`
- `IFormSchemaRepository` / `AzureFormSchemaRepository`
- `ITenantConfigRepository` / `AzureTableTenantConfigRepository`
- `IUserRepository` / `AzureTableUserRepository`

The rest of the application asks for domain-level operations like "get translations" or "prepare inspection ingest" instead of hand-writing Azure SDK calls everywhere.

### Why this pattern is used

It isolates storage-specific code, reduces duplication, and makes the application layer easier to test and reason about.

## 5. Decorator / Pipeline Pattern

### What it means

Some logic should run around every MediatR request, not inside individual handlers. MediatR pipeline behaviors do exactly that.

This is effectively a decorator chain around handler execution.

### How it looks here

Registered in `Services/ServiceCollectionExtensions.cs`:

- `UnhandledExceptionBehavior<,>`
- `LoggingBehavior<,>`
- `AuditBehavior<,>`
- `TransactionBehavior<,>`

Examples:

- `LoggingBehavior` records timing and logs request start/end.
- `AuditBehavior` emits audit events for important commands.
- `TransactionBehavior` wraps transactional requests.

### Why this pattern is used

It keeps logging, auditing, and transaction rules consistent without repeating the same code in every handler.

## 6. Outbox Pattern for Multi-Store Reliability

### What it means

The inspection ingest flow has to write to multiple systems:

- blob storage for payloads
- blob storage for uploaded files
- table storage for metadata and outbox state
- queue storage for downstream processing

Those writes are not wrapped in one distributed transaction. Instead, the system records progress in an outbox row and finishes the workflow in resumable steps.

### How it looks here

The key type is `Models/InspectionIngestOutboxEntity.cs`.

`AzureInspectionRepository.PrepareAsync(...)` stages the payload and files, updating flags like:

- `PayloadStaged`
- `FilesStaged`
- `MetadataWritten`
- `QueueMessageSent`
- `Completed`

`ProcessPendingAsync(...)` finishes the later steps and marks the ingest as complete.

### Why this pattern is used

Azure Blob, Table, and Queue operations cannot be made truly atomic together. The outbox pattern gives the system a durable record of what succeeded so it can safely resume after partial failure.

## 7. Compensating Action Pattern

### What it means

When part of a staged workflow fails, the code tries to undo the earlier steps instead of leaving half-finished data behind.

### How it looks here

In `AzureInspectionRepository`, if staging fails, `CompensateStagingAsync(...)` deletes the payload blob and any uploaded file blobs that were already written, then marks the outbox row as compensated.

### Why this pattern is used

This reduces the amount of orphaned or misleading data left behind after failure. It is a practical replacement for true rollback when the system spans multiple external services.

## 8. Background Worker / Polling Retry Pattern

### What it means

If the inspection finalize step cannot finish during the original HTTP request, the system does not fail the whole request. A background worker comes back later and retries pending work.

### How it looks here

`Services/InspectionIngestRetryHostedService.cs` runs in a loop:

1. create a DI scope
2. ask the repository for pending session ids
3. send `ProcessInspectionIngestCommand` for each one
4. wait and repeat

### Why this pattern is used

It makes the API more resilient. The client can get a success response for accepted work even if a downstream queue or metadata write is temporarily unavailable.

## 9. Optimistic Concurrency

### What it means

Instead of locking records before updates, the code checks whether the client is updating the version it thinks it is updating. If the version has changed, the update is rejected.

### How it looks here

The shared logic lives in `Application/Concurrency/OptimisticConcurrency.cs`.

It is used by mutation flows like:

- translations
- tenant config
- form schemas

The API expects `If-Match` headers and uses ETags to decide whether an update is still valid.

### Why this pattern is used

It prevents one client from silently overwriting another client's changes, while staying simple and compatible with Azure Table Storage semantics.

## 10. Strategy / Fallback Data Source Pattern

### What it means

Some features can read from one of two sources:

- the configured Azure repository
- an in-memory seed store loaded from bootstrap data

The application service chooses which source to use based on whether storage is configured.

### How it looks here

Examples:

- `TranslationApplicationService` chooses between `ITranslationRepository` and `ITranslationSeedStore`
- `FormSchemaApplicationService` chooses between `IFormSchemaRepository` and `IFormSchemaSeedStore`
- `TenantConfigApplicationService` follows the same idea

### Why this pattern is used

It lets the app run in lower-friction environments and still serve useful data even when backing storage is missing or intentionally disabled.

## 11. Composition Root + Dependency Injection

### What it means

Object creation is centralized instead of each class constructing its own dependencies.

### How it looks here

`Program.cs` and `Services/ServiceCollectionExtensions.cs` act as the composition root:

- bind options
- register Azure clients
- register repositories and application services
- register MediatR handlers and behaviors
- register hosted services

Feature-level registration extensions like `AddInspectionFeature()` keep startup wiring modular.

### Why this pattern is used

It makes dependencies explicit, keeps startup organized, and allows features to be assembled cleanly from small registrations.

## 12. Observer-style Instrumentation

### What it means

The code wraps important operations so it can observe duration, failure counts, and contextual details without mixing telemetry code directly into every call site.

### How it looks here

`IStorageOperationObserver` / `StorageOperationObserver` wraps storage operations and records:

- latency
- success vs failure
- Azure status codes
- Azure error codes

This wrapper is used throughout repository code before calling Azure SDK methods.

### Why this pattern is used

It gives consistent telemetry for storage dependencies and keeps observability logic centralized instead of duplicating it in every repository method.

## Which Patterns Matter Most?

If you only remember four patterns in this codebase, remember these:

1. Layered architecture keeps responsibilities separated.
2. MediatR command/query handling standardizes request flow.
3. The inspection ingest outbox is the main reliability pattern.
4. Pipeline behaviors carry logging, auditing, and transaction rules across all requests.

Those four explain most of the codebase's structure and most of its important tradeoffs.
