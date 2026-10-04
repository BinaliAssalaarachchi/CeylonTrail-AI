"""Fixed, deterministic Destination operations over trusted ASP.NET data."""

from dataclasses import dataclass
from datetime import date
from typing import Iterable, Optional

from .schemas import DestinationRequest, TrustedAttraction


class DestinationToolError(RuntimeError):
    """A bounded Destination operation could not process trusted input."""


@dataclass(frozen=True)
class DestinationToolResult:
    tool: str
    purpose: str
    result_summary: str
    attractions: tuple[TrustedAttraction, ...]
    explanations: dict[str, tuple[str, ...]] | None = None


class DestinationTools:
    """The allow-listed operations used by the production Destination agent."""

    def __init__(self, attractions: Iterable[TrustedAttraction]):
        self._attractions = tuple(attractions)

    def filter_by_district(self, request: DestinationRequest, attractions: Optional[Iterable[TrustedAttraction]] = None) -> DestinationToolResult:
        source = tuple(attractions if attractions is not None else self._attractions)
        public = tuple(item for item in source if item.is_active and item.status.casefold() == "approved")
        district = request.district.casefold() if request.district else None
        filtered = tuple(item for item in public if district is None or item.district.casefold() == district)
        constraint = f" in district {request.district}" if request.district else " across all districts"
        return DestinationToolResult(
            "filter_by_district",
            "Keep approved and active trusted attractions matching the requested district.",
            f"{len(public)} approved active candidate(s) → {len(filtered)}{constraint}.",
            filtered,
        )

    def filter_by_interests_or_category(self, request: DestinationRequest, attractions: Iterable[TrustedAttraction]) -> DestinationToolResult:
        source = tuple(attractions)
        category_ids = set(request.category_ids)
        interests = {value.casefold() for value in request.interests}
        filtered = tuple(
            item for item in source
            if (not category_ids or item.category_id in category_ids)
            and (not interests or item.category.casefold() in interests)
        )
        return DestinationToolResult(
            "filter_by_interests_or_category",
            "Match trusted attraction categories against requested interests and category IDs.",
            f"{len(source)} candidate(s) → {len(filtered)} interest/category match(es).",
            filtered,
        )

    def filter_by_budget(self, request: DestinationRequest, attractions: Iterable[TrustedAttraction]) -> DestinationToolResult:
        source = tuple(attractions)
        filtered = tuple(item for item in source if request.max_budget is None or item.price <= request.max_budget)
        constraint = f" within LKR {request.max_budget}" if request.max_budget is not None else " without a budget cap"
        return DestinationToolResult(
            "filter_by_budget",
            "Keep trusted attractions whose authoritative price satisfies the budget constraint.",
            f"{len(source)} candidate(s) → {len(filtered)}{constraint}.",
            filtered,
        )

    def check_date_availability(self, request: DestinationRequest, attractions: Iterable[TrustedAttraction]) -> DestinationToolResult:
        source = tuple(attractions)
        if request.date is None:
            filtered = source
            summary = f"No date supplied; retained {len(filtered)} candidate(s) without inventing availability."
        else:
            filtered = tuple(item for item in source if self._available_on(item, request.date))
            summary = f"{len(source)} candidate(s) → {len(filtered)} available on {request.date}."
        return DestinationToolResult(
            "check_date_availability",
            "Use only trusted experience slots with positive available capacity on the requested date.",
            summary,
            filtered,
        )

    def rank_candidates(self, request: DestinationRequest, attractions: Iterable[TrustedAttraction]) -> DestinationToolResult:
        source = tuple(attractions)
        ranked = tuple(sorted(source, key=lambda item: (-self.score(item, request), item.price, item.name.casefold(), item.attraction_id)))
        return DestinationToolResult(
            "rank_candidates",
            "Order eligible trusted attractions by constraint match score, price, name, and ID.",
            f"Ranked {len(ranked)} eligible candidate(s); limit is {request.limit}.",
            ranked,
        )

    def explain_match(self, request: DestinationRequest, attractions: Iterable[TrustedAttraction]) -> DestinationToolResult:
        source = tuple(attractions)
        category_ids = set(request.category_ids)
        interests = {value.casefold() for value in request.interests}
        explanations: dict[str, tuple[str, ...]] = {}
        for item in source:
            self.get_attraction_details(item.attraction_id)
            reasons = ["Approved and active attraction"]
            if request.district and item.district.casefold() == request.district.casefold():
                reasons.append("Located in preferred district")
            if item.category.casefold() in interests:
                reasons.append("Matches requested interest")
            if item.category_id in category_ids:
                reasons.append("Matches requested category")
            if request.max_budget is not None and item.price <= request.max_budget:
                reasons.append("Within stated budget")
            if request.date and self._available_on(item, request.date):
                reasons.append("Available on requested date")
            explanations[item.attraction_id] = tuple(reasons)
        return DestinationToolResult(
            "explain_match",
            "Produce concise reasons supported by the request and trusted attraction facts.",
            f"Prepared match explanations for {len(source)} eligible candidate(s).",
            source,
            explanations,
        )

    def get_attraction_details(self, attraction_id: str) -> TrustedAttraction:
        for attraction in self._attractions:
            if attraction.attraction_id == attraction_id:
                return attraction
        raise DestinationToolError("Attraction is not present in the trusted tool result set.")

    def get_attraction_schedule(self, attraction_id: str):
        return self.get_attraction_details(attraction_id).schedules

    def get_attraction_availability(self, attraction_id: str, requested_date: Optional[date] = None):
        slots = self.get_attraction_details(attraction_id).experience_slots
        return [slot for slot in slots if requested_date is None or slot.date == requested_date]

    @staticmethod
    def score(item: TrustedAttraction, request: DestinationRequest) -> int:
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

    @staticmethod
    def _available_on(attraction: TrustedAttraction, requested_date: date) -> bool:
        return any(slot.date == requested_date and slot.available_capacity > 0 for slot in attraction.experience_slots)
