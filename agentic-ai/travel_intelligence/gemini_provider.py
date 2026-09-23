"""Optional Google Gemini adapter for advisory M4 reasoning only."""

import json
from typing import Any

from .prompts import build_provider_prompt
from .providers import ProviderUnavailableError
from .schemas import ProviderRecommendation, TravelValidationInput


class GeminiRecommendationProvider:
    """Call Gemini for a narrow, non-authoritative recommendation proposal."""

    name = "gemini"

    def __init__(
        self,
        api_key: str,
        model: str,
        timeout_seconds: float = 4.0,
        client: Any = None,
    ) -> None:
        self.model = model
        self.timeout_seconds = timeout_seconds
        if client is not None:
            self._client = client
            return

        try:
            from google import genai
            from google.genai import types
        except ImportError as error:
            raise ProviderUnavailableError(
                "The google-genai SDK is unavailable."
            ) from error

        try:
            self._client = genai.Client(
                api_key=api_key,
                http_options=types.HttpOptions(
                    timeout=max(1, int(timeout_seconds * 1000))
                ),
            )
        except Exception as error:
            raise ProviderUnavailableError(
                "The Gemini client could not be initialized."
            ) from error

    def recommend(
        self,
        validation: TravelValidationInput,
        system_policy: str,
    ) -> ProviderRecommendation:
        try:
            response = self._client.models.generate_content(
                model=self.model,
                contents=build_provider_prompt(validation, system_policy),
                config={
                    "response_format": {
                        "text": {
                            "mime_type": "application/json",
                            "schema": ProviderRecommendation.model_json_schema(),
                        }
                    },
                    "temperature": 0.1,
                },
            )
        except Exception as error:
            raise ProviderUnavailableError(
                "Gemini provider request failed."
            ) from error

        response_text = getattr(response, "text", None)
        if not isinstance(response_text, str) or not response_text.strip():
            raise ProviderUnavailableError("Gemini returned an empty response.")

        try:
            payload = json.loads(response_text)
            return ProviderRecommendation.model_validate(payload)
        except Exception as error:
            raise ProviderUnavailableError(
                "Gemini returned an invalid structured response."
            ) from error
