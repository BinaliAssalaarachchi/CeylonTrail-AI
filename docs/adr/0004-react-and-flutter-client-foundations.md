# ADR 0004: React web and Flutter mobile client foundations

- Status: Accepted
- Date: 2026-09-19

## Context

CeylonTrail AI serves different client contexts: a web application for shared web workflows and a mobile application for the tourist-facing experience.

## Decision

Use React with Vite for the web foundation and Flutter for the mobile foundation. Both clients share the backend authentication contract but keep client-specific routing, storage, and presentation infrastructure.

## Consequences

The clients can evolve independently while using one backend boundary. Business functionality is intentionally deferred to focused feature branches.
