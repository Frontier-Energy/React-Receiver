# Configuration

This document describes the runtime configuration expected by React-Receiver.

## Configuration Sources

Configuration is bound through standard ASP.NET Core configuration providers. In practice that means values can come from:

- `appsettings.json`
- `appsettings.{Environment}.json`
- environment variables
- secret stores or deployment-time configuration injection

The application binds these sections:

- `BlobStorage`
- `QueueStorage`
- `TableStorage`
- `BootstrapData`

## BlobStorage

Used for:

- raw inspection payload blobs
- stored schema content where applicable

Configured fields:

- `ConnectionString`
- `ContainerName`

Notes:

- `ContainerName` is used for inspection payload blobs
- uploaded inspection files are stored in a separate container named by `StorageDependencyNames.FilesContainerName` (`Services/StorageDependencyNames.cs`)

## QueueStorage

Used for:

- inspection session queue messages after ingest staging succeeds

Configured fields:

- `ConnectionString`
- `QueueName`

## TableStorage

Used for:

- users
- current user profile
- inspection file metadata
- inspection ingest outbox state
- tenant configs
- form schema catalog
- form schemas
- translations

Configured fields:

- `ConnectionString`
- `TableName`
- `InspectionFilesTableName`
- `InspectionIngestOutboxTableName`
- `TenantConfigTableName`
- `MeTableName`
- `FormSchemaCatalogTableName`
- `FormSchemasTableName`
- `TranslationsTableName`

## BootstrapData

Configured fields:

- `SeedOnStartup`
- `OverwriteExisting`

Behavior:

- when `SeedOnStartup` is `true`, startup imports form schemas, translations, and tenant config through their application services
- `OverwriteExisting` controls whether import replaces existing data

## Startup Validation

Storage-related configuration is validated in two places:

1. option validators on startup
2. startup health checks

Relevant files:

- `Services/StorageOptionsValidators.cs`
- `Services/StorageHealthChecks.cs`
- `Services/StorageInfrastructureHostedService.cs`
- `Services/StartupHealthCheckHostedService.cs`

## Environment Variable Mapping

Examples:

```text
BlobStorage__ConnectionString
BlobStorage__ContainerName
QueueStorage__QueueName
TableStorage__InspectionIngestOutboxTableName
BootstrapData__SeedOnStartup
```

## Operational Recommendations

- Keep connection strings out of long-lived source-controlled defaults where possible.
- Treat the inspection files container defined in `StorageDependencyNames.FilesContainerName` as part of inspection ingest, even though it is not configured under its own section.
- If startup fails, inspect both option-validation failures and health-check failures.
