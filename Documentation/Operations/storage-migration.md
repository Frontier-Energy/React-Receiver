# Storage Migration

Use `scripts/Migrate-StorageToDev.ps1` to copy storage data from an existing account into the repo's `dev` environment.

## Prerequisites

- Azure CLI installed
- `az login` completed against the subscription that contains both storage accounts
- AzCopy installed and available on `PATH`

## Default migration

This copies from `receiverstorage` into the storage account found in `rg-qcontrol-service-cus-dev`:

```powershell
.\scripts\Migrate-StorageToDev.ps1
```

## Useful options

- Copy from a different source account:

```powershell
.\scripts\Migrate-StorageToDev.ps1 -SourceAccountName otherstorageaccount
```

- Target a different environment resource group:

```powershell
.\scripts\Migrate-StorageToDev.ps1 -DestinationEnvironment uat
```

- Override the destination storage account explicitly:

```powershell
.\scripts\Migrate-StorageToDev.ps1 -DestinationAccountName stqcscusdev123456
```

- Migrate only blobs:

```powershell
.\scripts\Migrate-StorageToDev.ps1 -SkipTables -SkipQueues
```

## Scope

The script migrates:

- blob containers: `raw-data`, `files`, `files-quarantine`
- queue: `processor`
- tables: `Users`, `InspectionFiles`, `InspectionIngestOutbox`, `TenantConfigs`, `MeProfiles`, `FormSchemaCatalog`, `FormSchemas`, `Translations`

## Queue caveat

Queue migration works by repeatedly receiving visible messages from the source queue and reposting them to the destination queue without deleting them from the source. For a complete point-in-time copy, stop queue writers and consumers before running the script.
