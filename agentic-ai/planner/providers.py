"""Model-provider boundary for the Planner Agent."""

import json
import os
from typing import Any, Protocol

from .schemas import PlannerInput, PlannerOutput


class PlannerProviderError(RuntimeError):
    def __init__(self, message: str, *, retryable: bool = True):
        super().__init__(message)
        self.retryable = retryable


class PlannerConfigurationError(PlannerProviderError):
    def __init__(self, message: str):
        super().__init__(message, retryable=False)


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
                    "response_json_schema": PlannerOutput.model_json_schema(),
                },
            )
        except Exception as error:
            message = str(error).lower()
            retryable = not any(
                marker in message
                for marker in ("401", "403", "api key", "quota", "resource exhausted")
            )
            raise PlannerProviderError(
                "Gemini Planner request failed.", retryable=retryable
            ) from error

        parsed = getattr(response, "parsed", None)
        if parsed is not None:
            return parsed
        text = getattr(response, "text", None)
        if not text:
            raise PlannerProviderError("Gemini Planner returned an empty response.")
        try:
            return json.loads(text)
        except json.JSONDecodeError as error:
            raise PlannerProviderError(
                "Gemini Planner returned malformed structured JSON."
            ) from error


def create_planner_provider() -> PlannerModelProvider:
    api_key = os.getenv("GEMINI_API_KEY", "").strip()
    if not api_key:
        return MissingPlannerProvider()
    return GeminiPlannerModelProvider(
        api_key=api_key,
        model=os.getenv("PLANNER_MODEL", "gemini-2.5-flash").strip()
        or "gemini-2.5-flash",
    )
