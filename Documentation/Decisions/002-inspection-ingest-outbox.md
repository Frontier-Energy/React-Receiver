# ADR 002: Inspection Ingest Uses An Application-Managed Outbox

## Status

Accepted

## Context

Receiving an inspection requires coordinated writes to:

- payload blob storage
- file blob storage
- inspection file metadata table
- queue storage

## Decision

Use `InspectionIngestOutboxEntity` as a durable progress record and split the workflow into:

1. staging
2. finalization
3. retry

## Consequences

Positive:

- resilient request acceptance
- resumable multi-store workflow
- support for background retry

Negative:

- eventual consistency becomes part of the API contract
- debugging requires understanding outbox state
