"""Provider boundary for optional future LLM-backed recommendations."""

from typing import Protocol

from .schemas import TravelRecommendationOutput, TravelValidationInput


class ProviderUnavailableError(RuntimeError):
    """Raised when an optional recommendation provider cannot respond."""


class RecommendationProvider(Protocol):
    def recommend(
        self,
        validation: TravelValidationInput,
        system_policy: str,
    ) -> TravelRecommendationOutput:
        """Return a schema-valid advisory recommendation."""
