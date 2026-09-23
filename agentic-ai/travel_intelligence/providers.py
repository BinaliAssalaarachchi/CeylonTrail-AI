"""Provider boundary and optional provider factory for M4 recommendations."""

import os
from typing import Mapping, Protocol

from .schemas import ProviderRecommendation, ToolRequest, TravelValidationInput


class ProviderUnavailableError(RuntimeError):
    """Raised when an optional recommendation provider cannot respond."""


class RecommendationProvider(Protocol):
    def select_tool(
        self,
        validation: TravelValidationInput,
        objective: str,
        current_step: str,
        allowed_tools: list[str],
        executed_tools: list[str],
    ) -> ToolRequest:
        """Propose one tool; the orchestrator remains the sole executor."""

    def recommend(
        self,
        validation: TravelValidationInput,
        system_policy: str,
    ) -> ProviderRecommendation:
        """Return only provider-authorable, schema-valid advisory data."""


def create_recommendation_provider(
    environ: Mapping[str, str] | None = None,
    provider_type=None,
) -> RecommendationProvider | None:
    """Create an optional provider without making credentials mandatory."""

    settings = environ if environ is not None else os.environ
    api_key = settings.get("GEMINI_API_KEY", "").strip()
    model = settings.get("GEMINI_MODEL", "").strip()
    if not api_key or not model:
        return None

    if provider_type is None:
        from .gemini_provider import GeminiRecommendationProvider

        provider_type = GeminiRecommendationProvider
    try:
        return provider_type(api_key=api_key, model=model)
    except ProviderUnavailableError:
        return None
