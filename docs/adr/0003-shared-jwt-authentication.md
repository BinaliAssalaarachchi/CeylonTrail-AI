# ADR 0003: Shared JWT authentication for React and Flutter

- Status: Accepted
- Date: 2026-09-19

## Context

The web and mobile clients need the same sign-in behavior and application roles while the backend remains the security authority.

## Decision

ASP.NET Core issues JWTs containing the authenticated identity and application role. React and Flutter call the shared login endpoint, persist tokens through client-appropriate mechanisms, and send Bearer tokens to the API.

## Consequences

Client-side role checks guide navigation, but backend authorization remains authoritative. JWT signing keys are configuration secrets and are never stored in source control.
