# ADR 004: Authentication Remains Lightweight And Repository-Driven

## Status

Accepted

## Context

React-Receiver currently needs basic user lookup and registration behavior, but it is not acting as a full identity platform.

## Decision

Keep auth lightweight:

- login is effectively an email lookup flow
- registration creates a user if needed
- current-user data can fall back to stored profile or default seeded behavior

## Consequences

Positive:

- simple integration model
- low implementation overhead

Negative:

- not a substitute for a full authentication and authorization platform
