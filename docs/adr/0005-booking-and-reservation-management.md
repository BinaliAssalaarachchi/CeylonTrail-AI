# ADR 0005: Booking and Reservation Management Architecture

- Status: Accepted
- Date: 2026-09-22

## Context

The CeylonTrail AI platform requires an end-to-end booking and reservation management subsystem (Member 3 slice). Tourists need to reserve attraction tickets, accommodation, and guided activities either manually or via AI-synthesized itineraries. Tourism providers and travel coordinators need to review, accept, or reject incoming reservations, while maintaining full auditability and role-based security.

## Decision

1. **Authoritative Persistence & EF Core Model**:
   - `Booking` entities are persisted in PostgreSQL via EF Core with dedicated `BookingItem` child entities.
   - Every status transition creates an immutable `BookingStatusHistory` record tracking timestamp, status change, and optional reason.
   - Cancellations are recorded in a dedicated `Cancellation` entity with cancellation reason, initiated-by user, and timestamp.
   - Decimal monetary precision is standardized to `precision: 18, scale: 2`.

2. **Deterministic State Machine**:
   - Status transitions follow strict rules in `BookingService.cs`:
     - `Pending` $\rightarrow$ `Confirmed` (by Provider/Coordinator/Admin)
     - `Pending` $\rightarrow$ `Rejected` (by Provider/Coordinator/Admin with required reason)
     - `Pending` / `Confirmed` $\rightarrow$ `Cancelled` (by Tourist or Coordinator/Admin)
     - `Confirmed` $\rightarrow$ `Completed` (by Provider/Coordinator/Admin)
   - Invalid transitions (e.g. attempting to cancel an already completed or rejected booking) throw `InvalidOperationException`.

3. **Role-Based Access Control**:
   - `Tourist`: Allowed to create bookings, view their own bookings (`/api/bookings/my`), view details of their own bookings, and cancel their own bookings.
   - `TourismProvider`: Allowed to view pending/active bookings, accept bookings, and reject bookings with reason.
   - `TravelCoordinator` & `Administrator`: Full access to view, accept, reject, and cancel bookings across the platform.

4. **Agentic AI Subsystem Boundary**:
   - The **Booking & Action Agent** in `agentic-ai/bookings/` translates approved itinerary slots into structured `CREATE_BOOKING_REQUEST` action payloads.
   - The agent executes deterministic validations (schedule overlap detection, budget limit validation, capacity check) and sets `requires_approval = true` for human-in-the-loop verification.
   - The agent **never** interacts directly with PostgreSQL; action payloads are submitted exclusively through the authoritative ASP.NET Core API.

5. **Client Applications**:
   - **React Web UI**: Serves staff/providers on the Role Dashboard with metrics cards, status filter tabs, 1-click Accept, Reject reason modal, and an audit Status History timeline modal.
   - **Flutter Mobile UI**: Serves tourists with tabbed views (Active, Confirmed, History), itemized price breakdowns, and cancellation dialogs.

## Consequences

- Business rules, authorization, and financial calculations remain strictly centralized in ASP.NET Core.
- The system is resilient against invalid state transitions and unauthorized booking manipulation.
- Full auditability is guaranteed across all booking lifecycles.
