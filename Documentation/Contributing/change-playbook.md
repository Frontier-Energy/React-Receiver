# Change Playbook

This document describes the default way to make changes in React-Receiver without fighting the existing design.

## Core Principle

Prefer small, feature-local changes that preserve the existing request flow:

`Controller -> MediatR -> Handler -> Application Service -> Repository`

## Standard Workflow For A Feature Change

1. Identify the controller endpoint or create a new one.
2. Add or update a MediatR command/query and handler.
3. Keep orchestration in the feature application service.
4. Keep Azure-specific details in the repository.
5. Add or update tests.
6. Update documentation when behavior or expectations changed.

## Working With Optimistic Concurrency

For mutable content such as:

- translations
- tenant config
- form schemas

Follow the existing pattern:

- return `ETag` on reads
- require `If-Match` for updates
- return `428` if missing
- return `412` if stale

## Working With Inspection Ingest

When changing inspection ingest:

- preserve idempotency for equivalent retries
- preserve outbox durability semantics
- keep staging and finalization separately resumable
- avoid pushing infrastructure creation into the request path
- consider retry and compensation behavior part of the feature contract

Read [../Features/inspection-ingest.md](../Features/inspection-ingest.md) before making nontrivial changes there.

## Review Checklist

Before submitting a change, verify:

- the endpoint contract is intentional
- new behavior has tests
- logs remain useful for production debugging
- configuration changes are documented
- async or retry behavior is still safe
- documentation still matches the code
