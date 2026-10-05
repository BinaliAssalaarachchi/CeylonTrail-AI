# ADR 0006: Controlled multi-agent workflow with human approval

- Status: Accepted
- Date: 2026-10-05

## Context

CeylonTrail AI uses four specialized Python Agentic AI services for itinerary planning, destination recommendation, booking-action proposals, and travel intelligence. These services are useful for structured analysis, but they must not become an untrusted source of truth or a direct path to changing tourism, booking, or approval data.

The platform therefore needs to coordinate multiple agent stages while preserving the authority of ASP.NET Core and PostgreSQL. It also needs to support safe failure, reproducible review, operational visibility, and human oversight before consequential actions are executed. A free-form chatbot conversation would not provide sufficient control over inputs, outputs, state transitions, authorization, or auditability.

## Decision

Use an ASP.NET Core-controlled, persisted, multi-stage workflow with explicit human approval boundaries.

1. **ASP.NET Core remains the orchestrator and authority.**
   - Clients call ASP.NET Core only; they do not call internal agents or PostgreSQL directly.
   - The backend authenticates and authorizes the caller, validates the request, loads authoritative data, coordinates the stages, and owns all state transitions.
   - PostgreSQL remains the system of record for workflows, stages, validation results, execution metadata, approvals, and business entities.

2. **Use four ordered agent roles with narrow responsibilities.**
   - **Planner:** produces a structured itinerary from trusted trip constraints and supplied candidates.
   - **Destination:** recommends from backend-supplied approved and active attractions, schedules, and availability.
   - **Booking/Action:** checks eligible future slots, capacity, dates, and budget, then produces booking proposals.
   - **Travel Intelligence:** analyses persisted validation and alert results and returns advisory recommendations.

   Agents receive bounded, structured requests and return structured responses. They do not query PostgreSQL, make authorization decisions, or directly create, update, approve, or cancel bookings and other consequential records.

3. **Pass trusted snapshots into agent stages.**
   - The backend selects and validates the data supplied to agents, including approved attractions, active status, schedules, availability, prices, trip constraints, itinerary items, and validation results.
   - Stage inputs and outputs are stored as bounded JSON snapshots so staff can review what each stage received and returned.
   - Agent output is treated as a proposal or advisory result and is revalidated by backend rules before it can affect platform state.

4. **Persist workflow and stage state explicitly.**
   - Each workflow records its status, current stage, timestamps, safe error information, and ordered stages.
   - Each stage records its agent role, sequence, attempt, status, input snapshot, output snapshot, and related execution or approval identifiers.
   - Only valid workflow transitions are allowed: pending, running, awaiting approval, completed, failed safely, or cancelled as applicable.
   - Agent failures are represented as safe, user-visible outcomes and do not partially execute an untrusted action.

5. **Require human approval for consequential actions.**
   - Booking/action proposals and travel-intelligence outcomes that require intervention create an approval request rather than executing automatically.
   - Only an authorized Travel Coordinator or Administrator can approve or reject the request through protected backend endpoints.
   - Approval decisions, comments, decision maker, and timestamps are persisted for auditability.
   - The approved action is rechecked against current ownership, dates, availability, capacity, prices, and other backend rules before execution.

6. **Keep travel intelligence advisory.**
   - Travel Intelligence may recommend proceeding, reviewing, or changing an itinerary and may identify affected items, risks, alerts, and safer alternatives.
   - It cannot override deterministic feasibility validation, blocking issues, authorization, workflow state, or approval requirements.

7. **Support deterministic local operation.**
   - Each agent exposes a typed FastAPI/Pydantic contract and may use an optional external model provider.
   - A deterministic local fallback remains the supported path when provider credentials are unavailable, allowing development, testing, and demonstrations without requiring an external LLM.

## Consequences

- Business rules, security, persistence, and consequential side effects remain centralized in ASP.NET Core.
- Agent recommendations are traceable through persisted stage snapshots, execution metadata, workflow status, and approval records.
- Staff can inspect, approve, reject, or safely recover from AI-assisted actions without granting agents database access.
- Changes in agent implementation or model provider do not change the public client contract or the system of record.
- The workflow is more auditable and safer than direct autonomous actions, but it introduces orchestration code, persisted workflow state, approval latency, and additional validation work.
- External model availability is not required for the core workflow, although model-backed responses may be richer when optional provider configuration is present.

## Implementation evidence

- Workflow persistence is implemented through `AgentWorkflow` and `AgentWorkflowStage` entities and the workflow persistence service.
- Protected workflow and approval APIs expose staff visibility and decision operations.
- The Planner, Destination, Booking/Action, and Travel Intelligence services communicate through structured HTTP contracts.
- Booking/action execution is proposal-only until an authorized approval is recorded and the backend revalidates the action.
- The React and Flutter clients display workflow, validation, safety, booking, and approval state returned by the authoritative API.
