# Inspection Ingest

Inspection ingest is the core workflow in React-Receiver. It is also the most important place to understand the service's reliability model.

## Why This Flow Matters

This workflow spans multiple Azure services:

- Blob Storage for payload JSON
- Blob Storage for uploaded files
- Table Storage for outbox state and file metadata
- Queue Storage for downstream processing messages

Those writes are not wrapped in one distributed transaction. The code therefore uses a staged outbox flow.

## Entry Point

HTTP entry point:

- `POST /inspections`

Controller:

- `Controllers/InspectionsController.cs`

Handler:

- `Application/Inspections/ReceiveInspectionCommandHandler.cs`

Application service:

- `Application/Inspections/InspectionApplicationService.cs`

Repository:

- `Infrastructure/Inspections/AzureInspectionRepository.cs`

## Request Contract

The endpoint consumes `multipart/form-data`.

Expected parts:

- `Payload`: JSON payload string
- `Files`: optional uploaded files

Operational limits:

- multipart body limit: `27,279,360` bytes
- payload limit: `65,536` UTF-8 bytes
- file count limit: `10`
- per-file limit: `10,485,760` bytes
- total uploaded file limit: `26,214,400` bytes
- allowed file extensions: `.jpg`, `.jpeg`, `.png`, `.pdf`, `.txt`
- allowed MIME types:
  - `.jpg` and `.jpeg`: `image/jpeg`, `image/jpg`
  - `.png`: `image/png`
  - `.pdf`: `application/pdf`
  - `.txt`: `text/plain`

The JSON payload maps to `ReceiveInspectionRequest` and includes:

- `sessionId`
- `userId`
- `name`
- `queryParams`

If `sessionId` is missing, the repository assigns a value during normalization.
That generated value makes the request non-idempotent from the caller's perspective.

## High-Level Flow

The full flow is:

1. parse request
2. normalize request
3. build file manifest
4. create or load an outbox row
5. stage payload blob
6. stage uploaded file blobs
7. try to finalize immediately
8. if immediate finalization fails, retry later in a background loop

## Stage 1: Request Parsing

`ReceiveInspectionRequestParser` turns the multipart `Payload` JSON and `Files` collection into a `ReceiveInspectionRequest`.

If the JSON is invalid or the payload exceeds the parser limit, the handler throws `RequestParsingException`, which is translated into a `400 Bad Request`.

## Stage 2: Outbox Creation Or Reuse

`PrepareAsync` creates or loads `InspectionIngestOutboxEntity`.

Important behavior:

- if the same caller-supplied `sessionId` already exists with the same normalized request, the existing outbox row is reused and the endpoint remains idempotent
- normalized equivalence means `userId`, `name`, query param key-value pairs, and the ordered file manifest match exactly after filename sanitization
- file bytes are not re-hashed for deduplication; equivalence is based on normalized metadata already stored in the outbox row
- if the same `sessionId` exists with different normalized payload or file metadata, the request is rejected with `409 Conflict`

## Stage 3: Staging Payload And Files

`PrepareAsync` stages:

1. payload JSON to `{ContainerName}/{sessionId}.json`
2. files to `files/{sessionId}-{sanitizedFileName}`

After each successful stage, it updates outbox flags.

Important outbox fields:

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
- `Status`

## Compensation On Staging Failure

If staging fails after partial success, the repository attempts compensation:

- delete staged payload blob
- delete any staged file blobs
- reset outbox completion flags
- mark status as `Compensated`
- store the last error message

## Stage 4: Finalization

Finalization is handled by `ProcessPendingAsync`.

Finalization steps:

1. acquire a lightweight lease on the outbox row
2. write inspection file metadata to Table Storage
3. send a queue message with the `sessionId`
4. mark the outbox row as completed

## Lease And Concurrency Model

Before finalization, the repository tries to acquire a lease by updating the outbox row with optimistic concurrency semantics.

Lease behavior:

- `Processing = true`
- `LockedUntilUtc = now + 2 minutes`
- status becomes `Processing`

If another worker already holds the lease, the current attempt exits without processing.

## Inline Attempt Then Background Retry

`InspectionApplicationService.ReceiveInspectionAsync` stages the request first, then immediately sends `ProcessInspectionIngestCommand`.

If that inline finalization fails:

- the API still returns success to the caller
- a warning is logged
- `InspectionIngestRetryHostedService` retries later

Retry hosted service behavior:

- polls every 10 seconds
- requests up to 25 pending session IDs at a time
- sends `ProcessInspectionIngestCommand` for each pending session
- logs explicit terminal failures when a session is poisoned or rejected

## Retry Timing

On finalization failure:

- `RetryCount` increments
- status becomes `PendingRetry`
- `NextAttemptAtUtc` is set using exponential backoff
- once `RetryCount` reaches the configured poison threshold, status becomes `Poisoned` and automatic retries stop

Current backoff implementation:

- `2^retryCount` seconds
- capped at 300 seconds

## Admin Surface

Operators can inspect and re-drive the outbox through:

- `GET /inspections/admin/outbox`
- `GET /inspections/admin/outbox/{sessionId}`
- `POST /inspections/admin/outbox/{sessionId}/replay`

Replay behavior:

- sessions with staged payload and file artifacts can be replayed
- terminal sessions require `force=true` in the replay request body
- compensated sessions remain inspect-only because the staged artifacts were removed during compensation

## What The Client Sees

Successful `POST /inspections` responses indicate acceptance, not full downstream completion.

That is intentional.

## Retrieval Paths

After data is staged and finalized:

- `GET /inspections/{sessionId}` returns payload plus file references
- `GET /inspections/{sessionId}/files/{fileName}` streams a specific uploaded file

## Troubleshooting Guide

### Request accepted but downstream processing did not happen

Check:

- outbox row for `Completed`
- `RetryCount`
- `LastError`
- `NextAttemptAtUtc`
- whether the retry hosted service is running
- queue configuration and queue availability

### Ingest stuck in processing

Check:

- `LockedUntilUtc`
- whether a previous process crashed while holding a lease
- application logs for retry-loop failures

## Design Tradeoff

The ingest flow prefers durability and resumability over synchronous all-or-nothing completion.
