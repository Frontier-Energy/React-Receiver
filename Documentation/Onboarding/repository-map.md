# Repository Map

This document explains where code lives and where to look first when making changes.

## Top-Level Layout

- `Program.cs`: application entry point and HTTP pipeline
- `Controllers/`: HTTP endpoints
- `Application/`: feature handlers, commands, queries, and application services
- `Infrastructure/`: Azure-backed repositories and storage implementations
- `Models/`: request, response, and persistence shapes
- `Services/`: startup, hosted services, storage configuration, and dependency registration
- `Middleware/`: request-level middleware such as correlation handling
- `Mediation/`: MediatR behaviors, exception mapping, and transaction abstractions
- `Observability/`: audit logging and storage operation instrumentation
- `Validation/`: request validation filter and validator registrations
- `Domain/`: domain-specific helpers
- `SeedData/`: bootstrap content used by seed stores and startup import
- `React-Receiver.Tests/`: unit and controller-oriented tests

## How A Typical Request Moves

The common runtime path is:

`Controller -> MediatR command/query -> Handler -> Application Service -> Repository -> Azure Storage`

## Feature Areas In `Application/`

- `Auth`: login and registration
- `Users`: user lookup and current-user profile
- `Inspections`: inspection receive, read, file retrieval, and background finalization
- `FormSchemas`: schema catalog and content management
- `Translations`: translation read and update
- `TenantConfig`: tenant configuration read and update
- `Concurrency`: shared optimistic concurrency logic

## What Lives In `Infrastructure/`

Infrastructure contains repository implementations behind feature interfaces.

Examples:

- `Infrastructure/Inspections/AzureInspectionRepository.cs`
- `Infrastructure/FormSchemas/AzureFormSchemaRepository.cs`
- `Infrastructure/Translations/AzureTableTranslationRepository.cs`
- `Infrastructure/TenantConfig/AzureTableTenantConfigRepository.cs`
- `Infrastructure/Users/AzureTableUserRepository.cs`

## What Lives In `Services/`

`Services/` contains startup and operational wiring.

Key files:

- `ServiceCollectionExtensions.cs`
- `StorageInfrastructureHostedService.cs`
- `StartupHealthCheckHostedService.cs`
- `BootstrapDataHostedService.cs`
- `InspectionIngestRetryHostedService.cs`

If something happens automatically in the background, start here.

## Validation And Tests

`Validation/RequestValidation.cs` wires a global validation filter and request validators.

`React-Receiver.Tests/Tests` contains focused tests for:

- exception handling
- request parsing
- controller behavior
- optimistic concurrency
- startup validation

## Where To Start For Common Changes

### Add a new endpoint

1. Add or update a controller action.
2. Add a MediatR command or query and handler.
3. Add or update an application service method if orchestration is needed.
4. Add repository behavior if persistence changes.
5. Add tests.

### Change storage behavior

Start with:

- repository interface
- repository implementation
- storage-related tests
- observability wrappers around Azure calls

### Debug startup failures

Start with:

- `Program.cs`
- `Services/ServiceCollectionExtensions.cs`
- hosted services in `Services/`
- [../Operations/configuration.md](../Operations/configuration.md)
