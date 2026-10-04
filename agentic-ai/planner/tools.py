"""Fixed, side-effect-free Planner operations over the trusted request snapshot."""

from dataclasses import dataclass
from decimal import Decimal

from .schemas import PlannerInput, PlannerOutput, validate_planner_output


class PlannerToolError(ValueError):
    """A bounded Planner inspection or validation operation failed."""


@dataclass(frozen=True)
class PlannerToolResult:
    tool: str
    purpose: str
    result_summary: str


class PlannerTools:
    """The only operations available to the production Planner execution plan."""

    @staticmethod
    def inspect_trip_constraints(request: PlannerInput) -> PlannerToolResult:
        if request.end_date < request.start_date:
            raise PlannerToolError("Trip end date must be on or after the start date.")
        expected_duration = (request.end_date - request.start_date).days + 1
        if request.duration != expected_duration:
            raise PlannerToolError("Trip duration must match the inclusive date range.")
        return PlannerToolResult(
            "inspect_trip_constraints",
            "Validate trusted trip dates, duration, budget, and preferences.",
            f"{request.duration} day(s), budget LKR {request.budget}, {len(request.interests)} interest(s).",
        )

    @staticmethod
    def inspect_candidate_attractions(request: PlannerInput) -> PlannerToolResult:
        ids = [candidate.id for candidate in request.candidate_attractions]
        if len(ids) != len(set(ids)):
            raise PlannerToolError("candidateAttractions must not contain duplicate IDs.")
        return PlannerToolResult(
            "inspect_candidate_attractions",
            "Inspect the trusted candidate allow-list used by the Planner.",
            f"{len(ids)} trusted candidate attraction(s) available.",
        )

    @staticmethod
    def calculate_budget_usage(request: PlannerInput, output: PlannerOutput) -> PlannerToolResult:
        item_total = sum(
            (item.estimated_cost for day in output.days for item in day.items),
            Decimal("0"),
        )
        if item_total != output.estimated_cost:
            raise PlannerToolError("estimatedCost must equal the sum of item costs.")
        if item_total > request.budget:
            raise PlannerToolError("estimatedCost must not exceed the trip budget.")
        return PlannerToolResult(
            "calculate_budget_usage",
            "Compare the generated itinerary cost with the trusted trip budget.",
            f"LKR {item_total} of LKR {request.budget} budget used.",
        )

    @staticmethod
    def check_schedule_conflicts(request: PlannerInput, output: PlannerOutput) -> PlannerToolResult:
        candidate_ids = {candidate.id for candidate in request.candidate_attractions}
        scheduled_ids: set[str] = set()
        for day in output.days:
            if not request.start_date <= day.date <= request.end_date:
                raise PlannerToolError("Itinerary day dates must fall within the trip dates.")
            previous_end = None
            for item in sorted(day.items, key=lambda value: value.start_time):
                if item.attraction_id not in candidate_ids:
                    raise PlannerToolError("Itinerary item references an unknown candidate attraction.")
                if item.attraction_id in scheduled_ids:
                    raise PlannerToolError("An attraction may only be scheduled once.")
                if previous_end is not None and item.start_time < previous_end:
                    raise PlannerToolError("Itinerary items must not overlap.")
                previous_end = item.end_time
                scheduled_ids.add(item.attraction_id)
        return PlannerToolResult(
            "check_schedule_conflicts",
            "Check itinerary dates, duplicate attractions, and overlapping times.",
            f"{len(scheduled_ids)} scheduled attraction(s); no conflicts found.",
        )

    @staticmethod
    def validate_plan_constraints(request: PlannerInput, output: PlannerOutput) -> PlannerToolResult:
        validate_planner_output(output, request)
        return PlannerToolResult(
            "validate_plan_constraints",
            "Apply the existing deterministic Planner output validator.",
            "Planner output passed trusted-context validation.",
        )

