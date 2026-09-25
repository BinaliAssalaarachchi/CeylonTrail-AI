"""Deterministic, bounded Destination Agent implementation."""

from decimal import Decimal

from .schemas import (
    DestinationCandidateAttraction,
    DestinationInput,
    DestinationOutput,
    DestinationSelection,
)


def _normalise(value: str | None) -> str:
    return value.strip().lower() if value else ""


def _contains(value: str | None, terms: set[str]) -> bool:
    normalised = _normalise(value)
    return bool(normalised and any(term in normalised or normalised in term for term in terms))


def filter_candidates(
    request: DestinationInput, requirement_reference: str, remaining_budget: Decimal | None = None
) -> list[DestinationCandidateAttraction]:
    """Return only candidates satisfying an authoritative per-requirement cost bound."""

    requirement = next(item for item in request.requirements if item.reference == requirement_reference)
    maximum = requirement.max_cost if requirement.max_cost is not None else request.budget
    if remaining_budget is not None:
        maximum = min(maximum, remaining_budget)
    return [candidate for candidate in request.candidate_attractions if candidate.price <= maximum]


def score_candidate(
    request: DestinationInput,
    requirement_reference: str,
    candidate: DestinationCandidateAttraction,
) -> tuple[int, int, int, Decimal, str]:
    """Score controlled domain fields only; descriptions are never instructions or scoring input."""

    requirement = next(item for item in request.requirements if item.reference == requirement_reference)
    interests = {_normalise(value) for value in request.interests}
    regions = {_normalise(value) for value in request.preferred_regions}
    categories = {_normalise(value) for value in requirement.preferred_categories}
    activity_type = _normalise(requirement.activity_type)
    district = _normalise(requirement.preferred_district)
    category = _normalise(candidate.category)
    candidate_district = _normalise(candidate.district)

    category_score = int(_contains(candidate.category, categories | interests))
    activity_score = int(bool(activity_type) and _contains(candidate.category, {activity_type}))
    district_score = int(bool(district) and candidate_district == district)
    region_score = int(candidate_district in regions if candidate_district else False)
    return (activity_score, category_score, district_score + region_score, candidate.price, str(candidate.attraction_id))


def _reasons(request: DestinationInput, requirement_reference: str, candidate: DestinationCandidateAttraction) -> list[str]:
    requirement = next(item for item in request.requirements if item.reference == requirement_reference)
    reasons: list[str] = []
    if requirement.activity_type and _contains(candidate.category, {_normalise(requirement.activity_type)}):
        reasons.append("activity_type_match")
    if candidate.category and _contains(candidate.category, {_normalise(value) for value in request.interests}):
        reasons.append("interest_match")
    if requirement.preferred_district and _normalise(candidate.district) == _normalise(requirement.preferred_district):
        reasons.append("district_match")
    if not reasons:
        reasons.append("budget_eligible")
    return reasons[:6]


class DestinationAgent:
    def select(self, request: DestinationInput) -> DestinationOutput:
        selected_ids: set = set()
        selections: list[DestinationSelection] = []
        unmatched: list[str] = []
        total_cost = Decimal("0")

        for requirement in request.requirements:
            eligible = filter_candidates(request, requirement.reference, request.budget - total_cost)
            ranked = sorted(
                (candidate for candidate in eligible if candidate.attraction_id not in selected_ids),
                key=lambda candidate: (
                    -score_candidate(request, requirement.reference, candidate)[0],
                    -score_candidate(request, requirement.reference, candidate)[1],
                    -score_candidate(request, requirement.reference, candidate)[2],
                    score_candidate(request, requirement.reference, candidate)[3],
                    score_candidate(request, requirement.reference, candidate)[4],
                ),
            )
            if not ranked:
                unmatched.append(requirement.reference)
                continue

            candidate = ranked[0]
            selected_ids.add(candidate.attraction_id)
            total_cost += candidate.price
            selections.append(
                DestinationSelection(
                    requirementReference=requirement.reference,
                    attractionId=candidate.attraction_id,
                    fitReasons=_reasons(request, requirement.reference, candidate),
                    explanation=f"Selected {candidate.name} from the approved candidate set.",
                )
            )

        if not selections:
            return DestinationOutput(
                workflowId=request.workflow_id,
                selections=[],
                unmatchedRequirements=unmatched or [requirement.reference for requirement in request.requirements],
                status="NoMatch",
                summary="No approved candidate satisfies the supplied destination requirements.",
            )

        return DestinationOutput(
            workflowId=request.workflow_id,
            selections=selections,
            unmatchedRequirements=unmatched,
            status="Selected",
            summary=f"Selected {len(selections)} approved destination candidate(s).",
        )
