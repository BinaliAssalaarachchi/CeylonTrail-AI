"""Deterministic, grounded Destination Agent workflow."""

from .schemas import (
    DestinationCandidate,
    DestinationExecutionRequest,
    DestinationOutput,
    DestinationRequest,
)
from .tools import DestinationTools


class DestinationAgentError(RuntimeError):
    def __init__(self, message: str, *, stage: str = "destination"):
        super().__init__(message)
        self.stage = stage


def validate_destination_output(output: DestinationOutput, request: DestinationRequest, source: list) -> DestinationOutput:
    source_by_id = {item.attraction_id: item for item in source}
    seen: set[str] = set()
    for candidate in output.candidates:
        if candidate.attraction_id in seen:
            raise ValueError("destination output contains duplicate attraction IDs")
        seen.add(candidate.attraction_id)
        trusted = source_by_id.get(candidate.attraction_id)
        if trusted is None:
            raise ValueError("destination output contains an unknown attraction ID")
        if not trusted.is_active or trusted.status.casefold() != "approved":
            raise ValueError("destination output contains a non-public attraction")
        if candidate.name != trusted.name or candidate.price != trusted.price:
            raise ValueError("destination output changed trusted attraction details")
        if candidate.category_id != trusted.category_id or candidate.district != trusted.district:
            raise ValueError("destination output changed trusted classification")
        expected_slots = [slot.model_dump(mode="json") for slot in trusted.experience_slots if not request.date or slot.date == request.date]
        actual_slots = [slot.model_dump(mode="json") for slot in candidate.availability]
        if actual_slots != expected_slots:
            raise ValueError("destination output changed deterministic availability")
    if len(output.candidates) > request.limit:
        raise ValueError("destination output exceeds requested limit")
    return output


class DestinationAgent:
    def recommend(self, execution: DestinationExecutionRequest) -> DestinationOutput:
        tools = DestinationTools(execution.trusted_attractions)
        request = execution.request
        matches = tools.search_attractions(request)
        ranked = sorted(matches, key=lambda item: (-self._score(item, request), item.price, item.name.casefold(), item.attraction_id))
        candidates = []
        for item in ranked[: request.limit]:
            reasons = ["approved and active attraction"]
            if request.district:
                reasons.append("district matches")
            if request.interests:
                reasons.append("interest category matches")
            if request.max_budget is not None:
                reasons.append("within maximum budget")
            if request.date:
                reasons.append("available on requested date")
            candidates.append(DestinationCandidate(
                attractionId=item.attraction_id,
                name=item.name,
                district=item.district,
                categoryId=item.category_id,
                category=item.category,
                price=item.price,
                openingHours=tools.get_attraction_schedule(item.attraction_id),
                availability=tools.get_attraction_availability(item.attraction_id, request.date),
                matchReasons=reasons,
                score=self._score(item, request),
            ))
        output = DestinationOutput(
            candidates=candidates,
            status="Success" if candidates else "NoResults",
            message=None if candidates else "No approved active attractions match the supplied constraints.",
        )
        return validate_destination_output(output, request, matches)

    @staticmethod
    def _score(item, request: DestinationRequest) -> int:
        score = 1
        if request.district and item.district.casefold() == request.district.casefold():
            score += 3
        if request.interests and item.category.casefold() in {interest.casefold() for interest in request.interests}:
            score += 3
        if request.max_budget is not None and item.price <= request.max_budget:
            score += 2
        if request.date:
            score += 2
        return score
