# ADR 003: Config And Content Updates Use Optimistic Concurrency

## Status

Accepted

## Context

Tenant config, form schemas, and translations are mutable shared resources.

## Decision

Use ETag-based optimistic concurrency:

- reads return `ETag`
- updates require `If-Match`
- missing preconditions return `428`
- stale preconditions return `412`

## Consequences

Positive:

- prevents silent overwrite of concurrent changes
- matches Azure Table Storage semantics well

Negative:

- clients must preserve and replay ETags correctly
