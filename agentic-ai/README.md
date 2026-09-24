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
deterministic fallback. The optional Gemini provider is enabled only when both
environment variables below are present:

    GEMINI_API_KEY=<local secret>
    GEMINI_MODEL=<supported Gemini model name>

The provider returns only a proposed action, concise summary, and rationale.
Deterministic validation, affected items, alternatives, safe windows, and
approval requirements remain authoritative. Gemini provider requests are
bounded to 10 seconds, while the ASP.NET outer Travel Intelligence request is
configured for 20 seconds. There are no automatic retries; provider failure or
timeout safely falls back to deterministic behavior. Tests do not require an
API key or network access.
Recommendations such as rescheduling, budget review, and conflict resolution
remain advisory and require human approval when consequential.

The internal FastAPI service exposes:

    GET  /health
    POST /travel-intelligence/analyze

Start it from the repository root with:

    cd agentic-ai
    uvicorn travel_intelligence.api:app --host 127.0.0.1 --port 8001

ASP.NET finds it through TravelIntelligence:BaseUrl and
TravelIntelligence:TimeoutSeconds configuration. The intended flow is:

    React / Flutter -> ASP.NET Core -> internal Python service

Clients must never call the Python service directly. The Python service uses
the deterministic fallback without an LLM key or external provider.

### Human approval workflow

When ASP.NET receives a recommendation whose `RequiresHumanApproval` flag is
true, it persists a pending approval request containing only the validated,
safe recommendation snapshot. Repeated analysis requests reuse the existing
pending request for that validation and requester. Travel Coordinators and
Administrators can approve or reject requests through the ASP.NET approval
endpoints; each decision records the authenticated user, timestamp, and
optional comment. Approval decisions do not execute bookings or mutate the
itinerary, and the deterministic validation result remains authoritative.

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
