"""LLM-backed Planner Agent with deterministic post-validation."""

from datetime import date, time, timedelta
from decimal import Decimal
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

    def __init__(self, message: str, *, stage: str = "pydantic_validation", diagnostic_message: str | None = None):
        super().__init__(message, stage=stage, diagnostic_message=diagnostic_message)


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
                return validate_planner_output(output, request)
            except ValueError as error:
                raise PlannerValidationError(
                    "Planner output failed deterministic validation.",
                    stage="deterministic_validation",
                    diagnostic_message=str(error)[:500],
                ) from error

        raise PlannerError("Planner model provider failed.")


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
