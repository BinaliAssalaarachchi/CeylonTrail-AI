# ADR 0001: ASP.NET Core as the authoritative API boundary

- Status: Accepted
- Date: 2026-09-19

## Context

CeylonTrail AI has React and Flutter clients plus persistence and future internal services. Clients need one stable public boundary.

## Decision

ASP.NET Core is the authoritative public backend. React and Flutter communicate through its HTTP API and do not access PostgreSQL or internal AI services directly.

## Consequences

Authentication, authorization, validation, and public contracts are centralized. Client applications remain replaceable while the backend owns persistence and security decisions.
