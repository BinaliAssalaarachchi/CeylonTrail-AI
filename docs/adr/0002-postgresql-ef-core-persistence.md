# ADR 0002: PostgreSQL with Entity Framework Core

- Status: Accepted
- Date: 2026-09-19

## Context

The shared backend needs relational persistence for authentication and future tourism modules.

## Decision

Use PostgreSQL with Entity Framework Core and the Npgsql provider. Database credentials remain in ASP.NET Core User Secrets during local development.

## Consequences

The backend receives provider-supported migrations, constraints, indexes, and testable data access. Schema changes are controlled through EF Core migrations and are not performed by clients.
