# CeylonTrail-AI

CeylonTrail-AI is an SE3090 Assignment 1 Sri Lankan tourism planning and coordination platform. Tourists discover approved attractions, manage trips, generate structured itineraries, review travel-safety and feasibility results, and prepare bookings. Providers, coordinators, and administrators manage operational tourism data through protected web workflows.

The implemented system combines a Flutter tourist application, a React/Vite web portal, an ASP.NET Core API, PostgreSQL, and four specialized Python Agentic AI services. A tourist submits a future trip objective; ASP.NET Core authenticates, persists, validates, and coordinates the workflow; agents return structured results; and the itinerary, validation, workflow, and approval state are shown to the client.

## Technology stack

- .NET 8, ASP.NET Core Web API, C#
- Entity Framework Core 8, Npgsql, and PostgreSQL
- JWT bearer authentication and role-based authorization
- React, Vite, Axios, JavaScript/JSX
- Flutter and Dart
- Python FastAPI/Pydantic agent services
- Optional Gemini provider support with deterministic local fallback
- Swagger/OpenAPI
- Git/GitHub and GitHub Actions
- Flutter packages used by the app: Dio, flutter_secure_storage, shared_preferences, google_maps_flutter, and url_launcher

## User roles

Authorization is enforced by ASP.NET Core controllers and services; client-side navigation is not the security boundary.

| Role | Responsibilities |
|---|---|
| Tourist | Discover approved attractions, manage owned trips/preferences, generate itineraries, view validation results, and manage eligible bookings. |
| Tourism Provider | Manage owned attractions, images, schedules, experience slots, availability, and incoming bookings. |
| Travel Coordinator | Review operational trips, reports, agent executions, alerts, workflows, and approvals. |
| Administrator | Manage platform-wide attraction approval/status operations, alerts, workflows, reports, and approvals. |

## Four business components

### M1 — Trip and AI itinerary planning

Trip CRUD, dates, budgets, preferences/objectives, itinerary generation, itinerary history, and owner-scoped itinerary access are implemented. The Planner Agent receives trusted trip constraints and candidates, then validates dates, references, duplicate attractions, item costs, totals, and budget limits.

### M2 — Attractions and experiences

The attractions module implements approved discovery, search, categories, provider-owned attractions, administrator approval/rejection/status operations, images, schedules, experience slots, availability, favourites, and provider management. The Destination Agent recommends only approved/active supplied attractions using controlled district, interest/category, budget, date, and availability filtering.

### M3 — Booking and reservation management

The booking module implements tourist booking creation, server-calculated totals, availability/capacity checks, booking history, cancellation/deletion rules, and provider/coordinator/administrator actions. The Booking/Action Agent is proposal-only: it checks trusted future slots, capacity, trip dates, and budget, and never directly mutates PostgreSQL or creates bookings.

### M4 — Travel validation and operations

The backend validates itinerary timing, budgets, alerts, overlaps, and other deterministic feasibility rules. Travel alerts, agent stages, execution metadata, safe results, reports, workflows, approvals, and approval decisions are persisted. Travel Intelligence returns advisory recommendations and cannot override backend feasibility, risk, blocking issues, or approval requirements.

## Full-stack architecture

~~~text
Flutter mobile app  ----\
                         > ASP.NET Core API ----> PostgreSQL / EF Core
React web application --/

ASP.NET Core
   |-- Planner Agent              :8002
   |-- Destination Agent         :8003
   |-- Booking/Action Agent      :8004
   `-- Travel Intelligence Agent :8001
~~~

Clients call ASP.NET Core only; they do not access PostgreSQL or internal agents directly. The backend controls authentication, authorization, trusted snapshots, deterministic validation, persistence, workflow transitions, and approval execution.

## Agentic AI workflow

1. The tourist submits a trip objective and constraints.
2. ASP.NET Core authenticates the request and checks ownership/role rules.
3. Workflow and stage state are persisted.
4. Planner creates a structured plan.
5. Destination recommends from trusted approved tourism data.
6. Booking/Action prepares eligible booking proposals.
7. Travel Intelligence evaluates the persisted validation state.
8. Backend rules validate budget, dates, availability, timing, alerts, and costs.
9. Stages, outputs, errors, execution metadata, and approval state are persisted.
10. Consequential actions can require an authorized coordinator or administrator.
11. The final status/result is returned to the client.

| Agent | Port | Responsibility |
|---|---:|---|
| Travel Intelligence | 8001 | Advisory analysis of structured validation results. |
| Planner | 8002 | Structured itinerary planning from trusted constraints/candidates. |
| Destination | 8003 | Controlled recommendations from approved/active attractions. |
| Booking/Action | 8004 | Availability, capacity, date, budget checks and proposals. |

This is a controlled multi-stage workflow, not merely a chatbot.

## Database and persistence

PostgreSQL is the system of record. ASP.NET Core accesses it through EF Core/Npgsql. Migrations cover authentication, attractions, bookings, trips, alerts, validation, travel intelligence, approvals, and workflows. The verified local database contains 27 public tables, including users, attractions, categories, schedules, experience/availability slots, trips, itineraries, bookings, alerts, validation records, agent executions/workflows, and approval records.

Connection strings, JWT signing keys, passwords, and API keys must be configured locally through user-secrets or environment variables and must never be committed.

## ASP.NET Core API

Implemented route groups include:

- /api/auth — registration and login
- /api/trips — trip CRUD, preferences, itineraries, generation, history, and latest workflow/intelligence results
- /api/attractions — discovery, search, categories, recommendations, favourites, provider operations, approvals/status, images, schedules, slots, and availability
- /api/bookings and /api/provider/bookings — booking operations, history, cancellation, availability, and provider actions
- /api/itinerary-validations — validation and Travel Intelligence execution
- /api/travel-alerts — alert lifecycle and filtering
- /api/agent-workflows and /api/travel-intelligence/executions — protected workflow/execution visibility
- /api/approval-requests — protected approval listing, approval, and rejection
- /api/staff/trips and /api/reports/overview — protected operational views
- /health and /health/ready — liveness and PostgreSQL readiness

Swagger/OpenAPI with JWT bearer security is enabled in Development at the local API's /swagger route.

## React web application

The React/Vite client provides login/session handling, role-protected routing, tourist discovery and trips, itinerary and bookings, provider attraction/booking management, coordinator dashboards, travel alerts, workflow/intelligence views, reports, approvals, and administrator attraction operations. It calls ASP.NET Core through VITE_API_BASE_URL; the example value is http://localhost:5027.

Verified checks:

- npm.cmd run lint passes with warnings only.
- npm.cmd run build passes.

## Flutter mobile application

The Flutter client is tourist-facing and includes authentication, secure JWT storage, Dio API access, trips, itinerary and booking-related views, travel-intelligence/safety views, itinerary maps through google_maps_flutter, and external navigation links through url_launcher. Configure its API with the API_BASE_URL Dart define; Android emulators normally use http://10.0.2.2:5027.

Flutter source and tests are present. Runtime verification was limited by the local Flutter/emulator environment and is not claimed as a completed runtime pass.

## Validation and security

Implemented safeguards include JWT issuer/audience/signing-key/lifetime validation; role authorization; restricted public registration; whitespace-only name/value rejection; DTO required-field and length limits; travel-alert date/content limits; approval-comment limits; past-slot rejection; itinerary date/time, cost, budget, duplicate, overlap, and ownership validation; planner duplicate-attraction and exact-total/over-budget validation; trusted approved/active snapshots; controlled tools and structured responses; safe agent fallback/errors; server-calculated booking totals; capacity, current-price, and ownership checks; approval for consequential actions; and image-proxy private-network/redirect/content/size protections.

## Local development

### Prerequisites

Install .NET 8 SDK, PostgreSQL, Node.js/npm, Python with agentic-ai/requirements.txt, and Flutter/Android tooling if needed. Configure the backend connection string and JWT signing key locally with .NET user-secrets.

### Backend

Use the local database ceylontrail_db, then:

~~~powershell
cd C:\Users\ASUS\Desktop\CeylonTrail-AI
dotnet restore backend\CeylonTrail.Api\CeylonTrail.Api.csproj
dotnet run --project backend\CeylonTrail.Api\CeylonTrail.Api.csproj --launch-profile http
~~~

The API uses port 5027. Local Swagger, health, and readiness URLs are http://localhost:5027/swagger, /health, and /health/ready.

### Python agents

Run each command in its own terminal from the repository root:

~~~powershell
uvicorn travel_intelligence.api:app --app-dir agentic-ai --host 127.0.0.1 --port 8001
uvicorn planner.api:app --app-dir agentic-ai --host 127.0.0.1 --port 8002
uvicorn destination.api:app --app-dir agentic-ai --host 127.0.0.1 --port 8003
uvicorn bookings.api:app --app-dir agentic-ai --host 127.0.0.1 --port 8004
~~~

Optional provider variable names include GEMINI_API_KEY, GEMINI_MODEL, and PLANNER_MODELS. Configure them locally only; deterministic fallback does not require them.

### React

~~~powershell
cd C:\Users\ASUS\Desktop\CeylonTrail-AI\web\ceylontrail-react
Copy-Item .env.example .env.local
npm install
npm.cmd run dev
~~~

The development server normally uses port 5173.

### Flutter

~~~powershell
cd C:\Users\ASUS\Desktop\CeylonTrail-AI\mobile\ceylontrail_flutter
flutter pub get
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5027
~~~

Do not commit machine-specific URLs or secrets.

## Testing and quality

- ASP.NET Core tests: 242 passed, 0 failed, 0 errors.
- Python Agentic AI suite: 101 tests previously verified, 0 failures, 0 errors.
- Live valid requests to all four agents succeeded; invalid request bodies returned HTTP 422.
- React lint passed with warnings only.
- React production build passed.
- Flutter source/tests are present; runtime verification was limited by the local SDK/emulator environment.

The backend suite covers authentication, authorization, CORS, image-proxy security, seeded data, trips, itineraries, attractions, bookings, availability, travel alerts, validation, agent boundaries, workflow persistence, approvals, ownership, safe fallback, and duplicate/over-budget protections.

## Demo flow

1. Start PostgreSQL, the four agents, ASP.NET Core on 5027, and React on 5173.
2. Login as tourist@test.com; demonstrate discovery, favourites, trip/objective entry, itinerary generation, validation/travel-intelligence, and eligible booking actions.
3. Login as provider@test.com; demonstrate owned attractions, schedules/availability, images, and incoming bookings.
4. Login as coordinator@test.com or admin@test.com; demonstrate dashboards, staff workflow visibility, alerts, execution details, attraction approval, and approval decisions.
5. Explain the backend-controlled multi-agent workflow and PostgreSQL persistence.
6. If Flutter/emulator is unavailable, use React; both clients use the same API.

See docs/DEMO_GUIDE.md for the detailed presentation sequence.

## Project structure

~~~text
backend/CeylonTrail.Api/       ASP.NET Core API, EF Core model, migrations, services, controllers
tests/CeylonTrail.Api.Tests/   Backend unit, service, authorization, workflow, security tests
web/ceylontrail-react/         React/Vite web client
mobile/ceylontrail_flutter/    Flutter/Dart client and widget/unit tests
agentic-ai/                    FastAPI/Pydantic agent services
database/                      Database-related material
docs/                          Demo guide and ADRs
docs/adr/                      Architecture Decision Records
.github/workflows/             GitHub Actions CI
~~~

## Engineering practices

The repository provides Git/GitHub branch and pull-request history, GitHub Actions backend restore/build/test automation, backend/Python tests, React lint/build checks, Flutter test sources, EF Core migrations, Swagger/OpenAPI, ADRs, structured validation, safe errors, ownership checks, and controlled AI-service boundaries.

## Deployment status

No production cloud deployment or public URL is claimed. The documented deployment is local:

- API: http://localhost:5027
- Swagger: http://localhost:5027/swagger
- Health: http://localhost:5027/health
- React development server: http://localhost:5173
- Internal agents: 127.0.0.1:8001 through 127.0.0.1:8004

## Demo accounts

| Email | Role |
|---|---|
| tourist@test.com | Tourist |
| provider@test.com | Tourism Provider |
| coordinator@test.com | Travel Coordinator |
| admin@test.com | Administrator |

Passwords and secrets are intentionally excluded.

## Documentation

- docs/DEMO_GUIDE.md — detailed demo guide
- docs/adr/ — Architecture Decision Records
- agentic-ai/README.md — agent service documentation
- mobile/ceylontrail_flutter/README.md — Flutter client notes
- .github/workflows/ci.yml — GitHub Actions CI
- backend/CeylonTrail.Api/Data/Migrations/ — EF migrations
- tests/CeylonTrail.Api.Tests/ — backend tests

## AI usage

AI-assisted tools were used for planning, coding assistance, debugging, documentation, and testing support. Suggestions were reviewed, tested, and adapted by the students. Individual prompts and personal contributions belong in the relevant individual report.

## Team contributions

| Member | Component | Agent responsibility |
|---|---|---|
| M1 | Trip and AI Itinerary Planning | Planner/Coordinator Agent |
| M2 | Attractions and Experiences | Destination Agent |
| M3 | Booking and Reservation Management | Booking/Action Agent |
| M4 | Travel Validation and Operations | Validation/Travel Intelligence Agent |

No student names or IDs are inferred here.

## Final status

CeylonTrail-AI is in a stable, demonstrable final state for the implemented web, backend, database, and Python Agentic AI workflow. Authentication, role authorization, core business components, workflow persistence, validation, approvals, and local execution are implemented. Flutter source and tests are present; Flutter runtime verification remains limited by the local SDK/emulator environment and is recorded honestly here.

