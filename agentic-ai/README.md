# CeylonTrail AI - Agentic AI Service

## Overview
This directory houses the autonomous agent components for the CeylonTrail platform.

### Member 3: Booking & Action Agent (`bookings/`)
The **Booking & Action Agent** is responsible for:
1. Translating approved tourist itinerary items and slots into structured booking action intents (`CREATE_BOOKING_REQUEST`).
2. Performing deterministic validations:
   - Schedule overlap detection between activities/attractions.
   - Budget constraint validation against tourist preferences.
   - Party size and capacity bounds checking.
3. Enforcing **human-in-the-loop** safety workflows (`requires_approval = True`).
4. Synthesizing structured payloads matching the ASP.NET Core `CreateBookingRequest` DTO schema.

---

### M4: Travel Intelligence & Validation Agent

The M4 agent consumes the structured ValidationResult produced by the
ASP.NET Core deterministic itinerary validator and returns advisory,
schema-validated recommendations. ASP.NET remains authoritative for
feasibility, risk, issue severity, and blocking state; the agent does not
re-evaluate or override those values.

The module uses Pydantic schemas and standard-library policy logic. It exposes
controlled deterministic helpers for issue summaries, blocking issues, risk,
affected items, and finite recommendation actions. It has no database,
filesystem, shell, booking, or itinerary-modification tools.

When no provider is configured, or a provider fails, the agent uses its
deterministic fallback. Provider configuration is intentionally not wired to a
real external service in this phase; a future provider can implement the
RecommendationProvider protocol without requiring API keys in tests.
Recommendations such as rescheduling, budget review, and conflict resolution
remain advisory and require human approval when consequential.

Run all agent tests from the repository root:

    python -m unittest discover -s agentic-ai -p "test_*.py"

---

## Architectural Boundaries
In adherence to the CeylonTrail Architecture Decision Records (ADRs):
- AI agents **never** communicate directly with the PostgreSQL database.
- AI agents output structured action requests that are submitted through the authoritative **ASP.NET Core Web API** endpoints.

---

## Testing
Run unit tests for the Booking & Action Agent:

```powershell
python -m unittest discover -s agentic-ai -p "test_*.py"
```
