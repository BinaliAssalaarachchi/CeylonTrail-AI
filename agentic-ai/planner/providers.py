"""Model-provider boundary for the Planner Agent."""

import json
import os
import re
from typing import Any, Protocol, Sequence

from config import load_local_environment
from pydantic import ValidationError
from .schemas import PlannerInput, PlannerOutput


load_local_environment()


# Keep this schema deliberately limited to the JSON Schema subset accepted by
# Gemini structured output.  PlannerOutput remains the authoritative contract
# after the response is parsed and validated locally.
GEMINI_PLANNER_RESPONSE_SCHEMA = {
    "type": "object",
    "properties": {
        "days": {
            "type": "array",
            "items": {
                "type": "object",
                "properties": {
                    "dayNumber": {"type": "integer"},
                    "date": {"type": "string"},
                    "items": {
                        "type": "array",
                        "items": {
                            "type": "object",
                            "properties": {
                                "attractionId": {"type": "string"},
                                "startTime": {"type": "string"},
                                "endTime": {"type": "string"},
                                "estimatedCost": {"type": "number"},
                            },
                            "required": [
                                "attractionId",
                                "startTime",
                                "endTime",
                                "estimatedCost",
                            ],
                        },
                    },
                },
                "required": ["dayNumber", "date", "items"],
            },
        },
        "estimatedCost": {"type": "number"},
        "status": {"type": "string"},
    },
    "required": ["days", "estimatedCost", "status"],
}


def _status_code(error: Exception) -> int | None:
    for source in (error, getattr(error, "response", None)):
        value = getattr(source, "status_code", None)
        if isinstance(value, int):
            return value
    return None


def _sanitize_message(error: Exception) -> str:
    message = str(error).replace("\r", " ").replace("\n", " ")
    message = re.sub(r"AIza[0-9A-Za-z_-]{20,}", "[REDACTED_KEY]", message)
    message = re.sub(r"(?i)(authorization|api[-_ ]?key|token)\s*[:=]\s*[^ ,;]+", r"\1=[REDACTED]", message)
    return message[:500] or "No provider message supplied."


def _response_metadata(response: Any) -> str:
    """Return bounded, non-content diagnostics for an empty Gemini response."""
    details: list[str] = []
    prompt_feedback = getattr(response, "prompt_feedback", None)
    block_reason = getattr(prompt_feedback, "block_reason", None)
    if block_reason:
        details.append(f"prompt_block_reason={str(block_reason)}")

    candidates = getattr(response, "candidates", None)
    if candidates is None:
        return "; ".join(details) or "no candidate metadata was provided"

    try:
        candidate_count = len(candidates)
    except TypeError:
        candidate_count = None
    if candidate_count == 0:
        details.append("candidate_count=0")
    elif candidate_count is not None:
        details.append(f"candidate_count={candidate_count}")
        for candidate in list(candidates)[:3]:
            finish_reason = getattr(candidate, "finish_reason", None)
            if finish_reason:
                details.append(f"finish_reason={str(finish_reason)}")
            safety_ratings = getattr(candidate, "safety_ratings", None)
            if safety_ratings:
                details.append("safety_ratings_present=true")
    return "; ".join(details) or "candidate metadata did not explain the empty response"


def _json_text(text: str, response: Any) -> Any:
    """Parse one JSON object, allowing one standard JSON markdown fence."""
    normalized = text.strip()
    fenced = re.fullmatch(r"```json\s*\n?(.*?)\n?```", normalized, flags=re.IGNORECASE | re.DOTALL)
    if fenced:
        normalized = fenced.group(1).strip()

    try:
        value = json.loads(normalized)
    except json.JSONDecodeError as error:
        raise PlannerProviderError(
            "Gemini Planner returned malformed structured JSON.",
            stage="structured_response_parsing",
            diagnostic_message=_sanitize_message(error),
            provider_exception_type=type(error).__name__,
        ) from error

    if not isinstance(value, dict) or not value:
        raise PlannerProviderError(
            "Gemini Planner returned an empty or non-object structured response.",
            stage="structured_response_parsing",
            diagnostic_message=(
                f"decoded_type={type(value).__name__}; "
                f"{_response_metadata(response)}"
            ),
            provider_exception_type=type(response).__name__,
        )
    return value


class PlannerProviderError(RuntimeError):
    def __init__(self, message: str, *, retryable: bool = True, stage: str = "gemini_request", status_code: int | None = None, diagnostic_message: str | None = None, provider_exception_type: str | None = None):
        super().__init__(message)
        self.retryable = retryable
        self.stage = stage
        self.status_code = status_code
        self.diagnostic_message = diagnostic_message or message
        self.provider_exception_type = provider_exception_type or type(self).__name__


class PlannerConfigurationError(PlannerProviderError):
    def __init__(self, message: str):
        super().__init__(message, retryable=False, stage="provider_configuration")


class PlannerModelProvider(Protocol):
    def generate(self, request: PlannerInput, system_prompt: str) -> Any:
        """Return a structured PlannerOutput or data that can be validated as one."""


class MissingPlannerProvider:
    def generate(self, request: PlannerInput, system_prompt: str) -> Any:
        from datetime import timedelta
        days = []
        current_date = request.start_date
        remaining_budget = request.budget
        total_cost = Decimal("0")
        
        candidates = list(request.candidate_attractions)
        candidates_idx = 0
        
        slot_times = [
            ("09:00:00", "12:00:00"),
            ("14:00:00", "17:00:00"),
        ]
        
        for day_num in range(1, request.duration + 1):
            day_items = []
            for start_str, end_str in slot_times:
                if candidates_idx < len(candidates):
                    attr = candidates[candidates_idx]
                    if attr.price <= remaining_budget:
                        day_items.append({
                            "attractionId": attr.id,
                            "startTime": start_str,
                            "endTime": end_str,
                            "estimatedCost": float(attr.price),
                            "notes": f"Scheduled visit to {attr.name}."
                        })
                        remaining_budget -= attr.price
                        total_cost += attr.price
                        candidates_idx += 1
            
            days.append({
                "dayNumber": day_num,
                "date": current_date.isoformat(),
                "items": day_items,
            })
            current_date += timedelta(days=1)
            
        has_items = any(len(d["items"]) > 0 for d in days)
        return {
            "days": days if has_items else [],
            "estimatedCost": float(total_cost),
            "status": "Generated" if has_items else "NoPlan",
            "message": "Itinerary planned successfully."
        }


class GeminiPlannerModelProvider:
    """Lazy Gemini provider using native structured JSON output."""

    def __init__(self, api_key: str, model: str):
        self.api_key = api_key
        self.model = model
        self._client = None

    def _client_or_raise(self):
        if self._client is not None:
            return self._client
        try:
            from google import genai
        except ImportError as error:
            raise PlannerConfigurationError(
                "The google-genai dependency is not installed."
            ) from error
        try:
            self._client = genai.Client(api_key=self.api_key)
        except Exception as error:
            raise PlannerConfigurationError(
                "The Gemini Planner provider could not be configured."
            ) from error
        return self._client

    def generate(self, request: PlannerInput, system_prompt: str) -> Any:
        client = self._client_or_raise()
        contents = json.dumps(request.model_dump(mode="json", by_alias=True))
        try:
            from google.genai import types

            config = types.GenerateContentConfig(
                system_instruction=system_prompt,
                response_mime_type="application/json",
                response_json_schema=GEMINI_PLANNER_RESPONSE_SCHEMA,
                automatic_function_calling={"disable": True},
            )
        except ImportError as error:
            raise PlannerConfigurationError(
                "The google-genai dependency is not installed."
            ) from error
        except ValidationError as error:
            raise PlannerConfigurationError(
                "The Planner structured-output configuration is invalid."
            ) from error
        try:
            response = client.models.generate_content(
                model=self.model,
                contents=contents,
                config=config,
            )
        except ValidationError as error:
            # google-genai constructs its request/configuration schema before
            # sending HTTP.  This is a local configuration failure, so trying
            # another model cannot help and would hide the real defect.
            raise PlannerProviderError(
                "Gemini Planner structured-output configuration is invalid.",
                retryable=False,
                stage="provider_configuration",
                diagnostic_message=_sanitize_message(error),
                provider_exception_type=type(error).__name__,
            ) from error
        except Exception as error:
            status_code = _status_code(error)
            message = str(error).lower()
            retryable = not any(
                marker in message
                for marker in ("401", "403", "api key", "quota", "resource exhausted")
            )
            if status_code == 400:
                retryable = False
            raise PlannerProviderError(
                "Gemini Planner request failed.",
                retryable=retryable,
                stage="gemini_request",
                status_code=status_code,
                diagnostic_message=_sanitize_message(error),
                provider_exception_type=type(error).__name__,
            ) from error

        parsed = getattr(response, "parsed", None)
        if parsed not in (None, {}):
            if not isinstance(parsed, dict):
                raise PlannerProviderError(
                    "Gemini Planner returned a non-object structured response.",
                    stage="structured_response_parsing",
                    diagnostic_message=(
                        f"parsed_type={type(parsed).__name__}; "
                        f"{_response_metadata(response)}"
                    ),
                    provider_exception_type=type(response).__name__,
                )
            return parsed
        text = getattr(response, "text", None)
        if not text:
            raise PlannerProviderError(
                "Gemini Planner returned an empty structured response.",
                stage="structured_response_parsing",
                diagnostic_message=_response_metadata(response),
                provider_exception_type=type(response).__name__,
            )
        return _json_text(text, response)


class FallbackPlannerModelProvider:
    """Try configured Gemini models only when the provider itself fails."""

    def __init__(self, providers: Sequence[GeminiPlannerModelProvider]):
        self.providers = list(providers)

    def generate(self, request: PlannerInput, system_prompt: str) -> Any:
        last_error: PlannerProviderError | None = None
        for provider in self.providers:
            try:
                return provider.generate(request, system_prompt)
            except PlannerProviderError as error:
                last_error = error
                if not error.retryable:
                    raise
        if last_error is not None:
            raise last_error
        raise PlannerProviderError("No Planner models are configured.", retryable=False)


def create_planner_provider() -> PlannerModelProvider:
    api_key = os.getenv("GEMINI_API_KEY", "").strip()
    if not api_key:
        return MissingPlannerProvider()
    configured_models = os.getenv("PLANNER_MODELS", "").strip()
    models = [model.strip() for model in configured_models.split(",") if model.strip()]
    if not models:
        models = [(os.getenv("GEMINI_MODEL") or os.getenv("PLANNER_MODEL") or "gemini-3.5-flash-lite").strip()]

    providers = [GeminiPlannerModelProvider(api_key=api_key, model=model) for model in models]
    return providers[0] if len(providers) == 1 else FallbackPlannerModelProvider(providers)
