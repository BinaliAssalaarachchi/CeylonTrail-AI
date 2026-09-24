"""Model-provider boundary for the Planner Agent."""

import json
import os
import re
from copy import deepcopy
from typing import Any, Protocol

from .schemas import PlannerInput, PlannerOutput


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


def _gemini_output_schema() -> dict[str, Any]:
    """Convert Pydantic JSON Schema to Gemini's supported JSON Schema subset."""
    source = PlannerOutput.model_json_schema()
    definitions = source.pop("$defs", {})
    supported = {
        "type", "format", "title", "description", "enum", "items", "minItems",
        "maxItems", "minimum", "maximum", "properties", "additionalProperties", "required",
    }

    def convert(node: Any) -> Any:
        if isinstance(node, list):
            return [convert(item) for item in node]
        if not isinstance(node, dict):
            return node
        reference = node.get("$ref")
        if reference:
            definition_name = reference.rsplit("/", 1)[-1]
            return convert(deepcopy(definitions[definition_name]))
        alternatives = node.get("anyOf")
        if alternatives:
            non_null = [item for item in alternatives if item.get("type") != "null"]
            has_null = len(non_null) != len(alternatives)
            if len(non_null) == 1:
                result = convert(non_null[0])
                if has_null and isinstance(result, dict) and isinstance(result.get("type"), str):
                    result["type"] = [result["type"], "null"]
                return result
            return {"anyOf": [convert(item) for item in alternatives]}
        result = {
            key: convert(value)
            for key, value in node.items()
            if key in supported
        }
        if result.get("type") == "object" and isinstance(result.get("properties"), dict):
            result["required"] = list(result["properties"].keys())
        return result

    return convert(source)


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
        raise PlannerConfigurationError(
            "GEMINI_API_KEY is not configured for the Planner Agent."
        )


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
            response = client.models.generate_content(
                model=self.model,
                contents=contents,
                config={
                    "system_instruction": system_prompt,
                    "response_mime_type": "application/json",
                    "response_json_schema": _gemini_output_schema(),
                    "automatic_function_calling": {"disable": True},
                },
            )
        except Exception as error:
            message = str(error).lower()
            retryable = not any(
                marker in message
                for marker in ("401", "403", "api key", "quota", "resource exhausted")
            )
            raise PlannerProviderError(
                "Gemini Planner request failed.",
                retryable=retryable,
                stage="gemini_request",
                status_code=_status_code(error),
                diagnostic_message=_sanitize_message(error),
                provider_exception_type=type(error).__name__,
            ) from error

        parsed = getattr(response, "parsed", None)
        if parsed not in (None, {}):
            return parsed
        text = getattr(response, "text", None)
        if not text:
            raise PlannerProviderError(
                "Gemini Planner returned an empty response.",
                stage="structured_response_parsing",
                diagnostic_message="Gemini response contained neither parsed data nor text.",
                provider_exception_type=type(response).__name__,
            )
        try:
            return json.loads(text)
        except json.JSONDecodeError as error:
            raise PlannerProviderError(
                "Gemini Planner returned malformed structured JSON.",
                stage="structured_response_parsing",
                diagnostic_message=_sanitize_message(error),
                provider_exception_type=type(error).__name__,
            ) from error


def create_planner_provider() -> PlannerModelProvider:
    api_key = os.getenv("GEMINI_API_KEY", "").strip()
    if not api_key:
        return MissingPlannerProvider()
    return GeminiPlannerModelProvider(
        api_key=api_key,
        model=os.getenv("PLANNER_MODEL", "gemini-3.6-flash").strip()
        or "gemini-3.6-flash",
    )
