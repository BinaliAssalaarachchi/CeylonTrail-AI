"""LLM-backed Planner Agent with deterministic post-validation."""

from datetime import date, time, timedelta
from decimal import Decimal
from time import perf_counter
from typing import Any

from pydantic import ValidationError

from .prompts import PLANNER_SYSTEM_POLICY
from .providers import (
    PlannerConfigurationError,
    PlannerModelProvider,
    PlannerProviderError,
    create_planner_provider,
)
from .schemas import (
    PlannerInput,
    PlannerOutput,
    validate_planner_output,
)
from .tools import PlannerToolError, PlannerTools
from agent_trace import AgentExecutionTrace, AgentTraceStep


class PlannerError(RuntimeError):
    """Base error exposed by the Planner Agent."""

    def __init__(self, message: str, *, stage: str = "planner", diagnostic_message: str | None = None, provider_exception_type: str | None = None, status_code: int | None = None):
        super().__init__(message)
        self.stage = stage
        self.diagnostic_message = diagnostic_message or message
        self.provider_exception_type = provider_exception_type or type(self).__name__
        self.status_code = status_code


class PlannerValidationError(PlannerError):
    """The model returned data that failed schema or trusted-context checks."""

    def __init__(self, message: str, *, stage: str = "pydantic_validation", diagnostic_message: str | None = None, trace: AgentExecutionTrace | None = None):
        super().__init__(message, stage=stage, diagnostic_message=diagnostic_message)
        self.trace = trace


class PlannerAgent:
    def __init__(
        self,
        provider: PlannerModelProvider | None = None,
        *,
        max_retries: int = 1,
    ):
        self.provider = provider or create_planner_provider()
        self.max_retries = max(0, max_retries)

    def generate(self, request: PlannerInput) -> PlannerOutput:
        started = perf_counter()
        attempts = self.max_retries + 1
        for attempt in range(attempts):
            try:
                raw = self.provider.generate(request, PLANNER_SYSTEM_POLICY)
                output = raw if isinstance(raw, PlannerOutput) else PlannerOutput.model_validate(raw)
            except PlannerConfigurationError:
                raise
            except PlannerProviderError as error:
                if error.retryable and attempt + 1 < attempts:
                    continue
                raise PlannerError(
                    "Planner model provider failed.",
                    stage=error.stage,
                    diagnostic_message=error.diagnostic_message,
                    provider_exception_type=error.provider_exception_type,
                    status_code=error.status_code,
                ) from error
            except (ValidationError, TypeError, ValueError) as error:
                if attempt + 1 < attempts:
                    continue
                raise PlannerValidationError(
                    "Planner model output failed schema validation.",
                    stage="pydantic_validation",
                    diagnostic_message=str(error)[:500],
                ) from error

            try:
                output = normalize_planner_output(output)
                trace = self._execute_tools(request, output, started)
                return output.model_copy(update={"trace": trace})
            except (PlannerToolError, ValueError) as error:
                trace = getattr(error, "trace", None)
                raise PlannerValidationError(
                    "Planner output failed deterministic validation.",
                    stage="deterministic_validation",
                    diagnostic_message=str(error)[:500],
                    trace=trace,
                ) from error

        raise PlannerError("Planner model provider failed.")

    @staticmethod
    def _execute_tools(request: PlannerInput, output: PlannerOutput, started: float) -> AgentExecutionTrace:
        operations = [
            lambda: PlannerTools.inspect_trip_constraints(request),
            lambda: PlannerTools.inspect_candidate_attractions(request),
            lambda: PlannerTools.calculate_budget_usage(request, output),
            lambda: PlannerTools.check_schedule_conflicts(request, output),
            lambda: PlannerTools.validate_plan_constraints(request, output),
        ]
        steps: list[AgentTraceStep] = []
        for sequence, operation in enumerate(operations, start=1):
            operation_started = perf_counter()
            try:
                result = operation()
            except (PlannerToolError, ValueError) as error:
                failure = AgentExecutionTrace(
                    agent="Planner",
                    responsibility="Create a feasible itinerary from trusted trip inputs.",
                    inputSummary=f"{request.duration} day(s), budget LKR {request.budget}, {len(request.candidate_attractions)} candidate(s).",
                    steps=steps,
                    decision="Planner output rejected safely.",
                    validation="Failed deterministic Planner validation.",
                    safeFailure=str(error)[:500],
                    durationMs=max(0, round((perf_counter() - started) * 1000)),
                )
                error.trace = failure  # type: ignore[attr-defined]
                raise
            steps.append(AgentTraceStep(
                sequence=sequence,
                tool=result.tool,
                purpose=result.purpose,
                status="Completed",
                resultSummary=result.result_summary,
                durationMs=max(0, round((perf_counter() - operation_started) * 1000)),
            ))

        return AgentExecutionTrace(
            agent="Planner",
            responsibility="Create a feasible itinerary from trusted trip inputs.",
            inputSummary=f"{request.duration} day(s), budget LKR {request.budget}, {len(request.candidate_attractions)} candidate(s).",
            steps=steps,
            decision="Generated feasible itinerary." if output.status == "Generated" else "No feasible itinerary generated.",
            validation="Passed deterministic Planner validation.",
            outputSummary=f"{len(output.days)} day(s), LKR {output.estimated_cost} estimated cost.",
            durationMs=max(0, round((perf_counter() - started) * 1000)),
        )


def normalize_planner_output(output: PlannerOutput) -> PlannerOutput:
    """Keep the first scheduled occurrence of each attraction ID.

    Provider output is treated as a proposal. Duplicate attractions are
    skipped deterministically before the unchanged validation tools run. Day
    entries are preserved, including empty days, and the authoritative total
    is recalculated from the retained items.
    """

    selected_ids: set[str] = set()
    total = Decimal("0")
    normalized_days = []

    for day in output.days:
        retained_items = []
        for item in day.items:
            if item.attraction_id in selected_ids:
                continue
            selected_ids.add(item.attraction_id)
            retained_items.append(item)
            total += item.estimated_cost
        normalized_days.append(day.model_copy(update={"items": retained_items}))

    return output.model_copy(
        update={
            "days": normalized_days,
            "estimated_cost": total,
        }
    )


class DeterministicPlannerFixture:
    """Small deterministic fixture for tests; never selected by production wiring."""

    def generate(self, request: PlannerInput, system_prompt: str = "") -> PlannerOutput:
        if not request.candidate_attractions:
            return PlannerOutput(
                days=[], estimatedCost=Decimal("0"), status="NoPlan", message="No candidates available."
            )

        interests = {value.lower() for value in request.interests}
        regions = {value.lower() for value in request.preferred_regions}
        ranked = sorted(
            request.candidate_attractions,
            key=lambda candidate: (
                -(2 if candidate.category and candidate.category.lower() in interests else 0)
                - (1 if candidate.region and candidate.region.lower() in regions else 0),
                candidate.price,
                candidate.id,
            ),
        )
        selected = []
        total = Decimal("0")
        for candidate in ranked:
            if total + candidate.price > request.budget:
                continue
            selected.append(candidate)
            total += candidate.price
            if len(selected) >= request.duration * 3:
                break

        if not selected:
            return PlannerOutput(
                days=[], estimatedCost=Decimal("0"), status="NoPlan", message="No candidates fit the budget."
            )

        days = []
        for index in range(0, len(selected), 3):
            day_date = request.start_date + timedelta(days=index // 3)
            items = []
            for slot, candidate in enumerate(selected[index:index + 3]):
                start_hour = 9 + slot * 3
                items.append({
                    "attractionId": candidate.id,
                    "startTime": time(start_hour, 0),
                    "endTime": time(start_hour + 2, 0),
                    "estimatedCost": candidate.price,
                    "notes": "Deterministic test fixture.",
                })
            days.append({"dayNumber": index // 3 + 1, "date": day_date, "items": items})

        output = PlannerOutput(days=days, estimatedCost=total, status="Generated")
        return validate_planner_output(output, request)
