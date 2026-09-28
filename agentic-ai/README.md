# CeylonTrail AI - Agentic AI Service

## Overview
This directory houses the autonomous agent components for the CeylonTrail platform.

### Member 3: Booking & Action Agent (`bookings/`)
The production FastAPI boundary is proposal-only. It receives a trusted
snapshot of M3 `AvailabilitySlot` records from ASP.NET, checks future times,
capacity, trip dates, and remaining budget, and returns structured proposals.
It never calls PostgreSQL, `BookingService`, or any mutation endpoint.

Endpoints:

    GET  /health
    POST /booking/prepare

Start it independently on port 8004:

    cd agentic-ai
    uvicorn bookings.api:app --host 127.0.0.1 --port 8004

ASP.NET finds it through `BookingActionAgent:BaseUrl` and
`BookingActionAgent:TimeoutSeconds`. `requiresApproval` is true for prepared
proposals; no booking or capacity reservation occurs.

The older dataclass-based helper remains available for compatibility with its
existing unit tests, but it is not used by the production FastAPI boundary.

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

    uvicorn travel_intelligence.api:app --app-dir agentic-ai --host 127.0.0.1 --port 8001

ASP.NET finds it through TravelIntelligence:BaseUrl and
TravelIntelligence:TimeoutSeconds configuration. The intended flow is:

    React / Flutter -> ASP.NET Core -> internal Python service

Clients must never call the Python service directly. The Python service uses
the deterministic fallback without an LLM key or external provider.

### Member 1: Planner Agent (`planner/`)

The Planner Agent generates structured tourist itineraries from trusted trip
dates, budget, preferences, and ASP.NET-supplied candidate attractions.
ASP.NET remains authoritative for ownership, validation, status transitions,
and persistence. The Planner Agent has no database access and never receives
direct client traffic.

Endpoints:

    GET  /health
    POST /planner/generate

Start it independently on port 8002:

    uvicorn planner.api:app --app-dir agentic-ai --host 127.0.0.1 --port 8002

Configuration variables:

    GEMINI_API_KEY   Required for actual generation
    GEMINI_MODEL     Optional Gemini model name; defaults to gemini-3.5-flash-lite
    PLANNER_MODELS   Optional comma-separated fallback model list

For local development, put those values in `agentic-ai/.env` (the file is
ignored by Git). Existing process/deployment environment variables take
precedence over `.env`; `PLANNER_MODEL` remains supported as a legacy fallback.
`PLANNER_MODELS` is used only for retryable provider/model failures; strict
request, output, and deterministic validation failures are never hidden by a
fallback model.

The Planner uses Gemini structured JSON output followed by deterministic
schema and trusted-context validation. ASP.NET calls it through
`PlannerAgent:BaseUrl`, configured as `http://localhost:8002`.

### Member 2: Destination Agent (`destination/`)

The Destination Agent recommends only approved, active attractions supplied by
ASP.NET's authoritative `IAttractionService`. It exposes `GET /health` and
`POST /destination/recommend` on port 8003. Its controlled tools perform
district, category, budget, and date/availability filtering and its final
output is checked against the trusted source records. ASP.NET exposes the
client-facing `POST /api/attractions/recommendations`; React and Flutter never
call the Python service directly.

### Development/demo data

Development startup seeds a small idempotent Sri Lankan dataset through
`DevelopmentDataSeeder`: eight approved active attractions across historical,
nature, adventure, and coastal categories, with weekly schedules and one
future experience/availability slot per attraction. The controlled Sigiriya
record remains stable at attraction
`55555555-5555-5555-5555-555555555555`, slot
`77777777-7777-7777-7777-777777777777`, and authoritative price LKR 6500.
The seeder does not create bookings and never overwrites existing records.
Public Discover results come from `GET /api/attractions` and are limited to
approved, active records; staff management uses the protected operational
routes.

### Local startup order

1. Start PostgreSQL and apply the existing EF migrations.
2. Start the four internal services from the repository root:

       uvicorn travel_intelligence.api:app --app-dir agentic-ai --host 127.0.0.1 --port 8001
       uvicorn planner.api:app --app-dir agentic-ai --host 127.0.0.1 --port 8002
       uvicorn destination.api:app --app-dir agentic-ai --host 127.0.0.1 --port 8003
       uvicorn bookings.api:app --app-dir agentic-ai --host 127.0.0.1 --port 8004

3. Start ASP.NET Core with its configured PostgreSQL connection and JWT
   signing key. Its health endpoints are `/health` and `/health/ready`; Swagger
   is available at `/swagger` in Development.

The golden approval flow has two phases: itinerary generation persists a
proposal and reaches `AwaitingApproval` without a confirmed booking; an
authorized coordinator or administrator then approves or rejects it through
ASP.NET. Approval reloads current attraction, slot, capacity, and price state
before creating or reusing the authoritative booking.

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
