# Bootstrap Data

React-Receiver includes checked-in bootstrap data at:

- `SeedData/bootstrap-data.json`

This file supports lower-friction local environments and startup seeding.

## What It Contains

The document currently has three top-level collections:

- `formSchemas`
- `translations`
- `tenantConfigs`

Examples currently included:

- form types such as `hvac`, `electrical`, and `electrical-sf`
- languages such as `en` and `es`
- tenants such as `qhvac` and `lire`

## How The App Uses It

There are two main uses.

### 1. Startup import

`BootstrapDataHostedService` imports this file through feature application services when:

- `BootstrapData:SeedOnStartup = true`

It imports:

- form schemas
- translations
- tenant config

### 2. Seed-store fallback

Some application services can choose between:

- repository-backed storage
- in-memory seed-store data

## File Loading Behavior

`FileBootstrapDataProvider` loads the JSON from `SeedData/bootstrap-data.json`.

The file is copied to the output directory so it is available at runtime.

## Safe Editing Guidelines

- keep JSON valid and human-readable
- preserve realistic `etag` and `version` values for seeded content
- keep tenant-form relationships consistent with available form schemas
- avoid environment-specific secrets or deployment-only values

## Recommended Improvement

A future enhancement would be to add a dedicated bootstrap-data contract test or JSON schema so malformed seed data fails faster.
