# ADR 001: Azure-Native Storage Primitives

## Status

Accepted

## Context

React-Receiver persists data across several concerns:

- inspection payloads
- uploaded files
- user and profile data
- tenant configuration
- form schemas
- translations
- downstream processing coordination

## Decision

Use Azure Blob Storage, Azure Table Storage, and Azure Queue Storage directly instead of introducing:

- a relational database
- a separate message broker
- a more elaborate distributed transaction layer

## Consequences

Positive:

- small infrastructure footprint
- direct alignment with Azure deployment environments
- simple storage model for payloads and files

Negative:

- no cross-store atomic transaction
- more application-managed consistency logic
