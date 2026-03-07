# Testing

This document explains how testing is organized in React-Receiver and what to add when you change behavior.

## Test Project

The main test project is:

- `React-Receiver.Tests/React-Receiver.Tests.csproj`

It targets `net10.0` and references the main web project directly.

## Run Tests

From the repo root:

```powershell
dotnet test
```

Run a single test file by filter:

```powershell
dotnet test --filter "FullyQualifiedName~ReceiveInspectionTests"
```

## What The Current Tests Cover

Examples from `React-Receiver.Tests/Tests`:

- `ReceiveInspectionTests.cs`: multipart parsing and inspection receive behavior
- `SchemaEndpointsTests.cs`: controller behavior for form schemas, translations, and current user
- `TenantConfigTests.cs`: tenant-config feature behavior
- `LoginTests.cs`: login handling
- `RegisterRequestHandlerTests.cs`: registration behavior
- `RequestContractValidationTests.cs`: request-contract expectations
- `ApiExceptionHandlerTests.cs`: exception-to-problem-details mapping
- `OptimisticConcurrencySeedStoreTests.cs`: ETag and `If-Match` behavior
- `StorageStartupValidationTests.cs`: startup validation and hosted-service assumptions

## Recommended Testing Heuristics

- Test behavior, not just implementation details.
- When a controller writes headers, assert the headers explicitly.
- When `If-Match` semantics are involved, test create, update, missing-header, and stale-header scenarios.
- When changing hosted services or startup wiring, add a guardrail test if possible.

## Gaps To Keep In Mind

The existing tests provide good coverage for request handling and startup guardrails, but there is still room to improve coverage around:

- deeper repository behavior against storage abstractions
- inspection retry edge cases
- hosted-service interaction over longer-running scenarios
