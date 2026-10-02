"""Deterministic, grounded Destination Agent workflow."""

from time import perf_counter

from .schemas import (
    DestinationCandidate,
    DestinationExecutionRequest,
    DestinationOutput,
    DestinationRequest,
)
from .tools import DestinationTools
from agent_trace import AgentExecutionTrace, AgentTraceStep


class DestinationAgentError(RuntimeError):
    def __init__(self, message: str, *, stage: str = "destination"):
        super().__init__(message)
        self.stage = stage
        self.trace: AgentExecutionTrace | None = None


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
        started = perf_counter()
        tools = DestinationTools(execution.trusted_attractions)
        request = execution.request
        steps: list[AgentTraceStep] = []

        district_result = tools.filter_by_district(request)
        steps.append(self._step(1, district_result, started))
        interests_result = tools.filter_by_interests_or_category(request, district_result.attractions)
        steps.append(self._step(2, interests_result, started))
        budget_result = tools.filter_by_budget(request, interests_result.attractions)
        steps.append(self._step(3, budget_result, started))
        availability_result = tools.check_date_availability(request, budget_result.attractions)
        steps.append(self._step(4, availability_result, started))
        ranking_result = tools.rank_candidates(request, availability_result.attractions)
        steps.append(self._step(5, ranking_result, started))
        explanation_result = tools.explain_match(request, ranking_result.attractions)
        steps.append(self._step(6, explanation_result, started))

        matches = explanation_result.attractions
        candidates = []
        for item in matches[: request.limit]:
            reasons = list((explanation_result.explanations or {}).get(item.attraction_id, ()))
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
                score=tools.score(item, request),
            ))
        output = DestinationOutput(
            candidates=candidates,
            status="Success" if candidates else "NoResults",
            message=None if candidates else "No approved active attractions match the supplied constraints.",
            trace=AgentExecutionTrace(
                agent="Destination",
                responsibility="Match the traveller's objective and constraints with trusted CeylonTrail attractions and experiences.",
                inputSummary=f"{len(execution.trusted_attractions)} trusted attraction(s), limit {request.limit}.",
                steps=steps,
                decision="Ranked matching destinations." if candidates else "No trusted destination satisfied all constraints.",
                validation="Passed trusted-data grounding and deterministic output validation.",
                outputSummary=f"{len(candidates)} recommendation(s) returned.",
                durationMs=max(0, round((perf_counter() - started) * 1000)),
            ),
        )
        return validate_destination_output(output, request, matches)

    @staticmethod
    def _step(sequence: int, result, started: float) -> AgentTraceStep:
        return AgentTraceStep(
            sequence=sequence,
            tool=result.tool,
            purpose=result.purpose,
            status="Completed",
            resultSummary=result.result_summary,
            durationMs=max(0, round((perf_counter() - started) * 1000)),
        )
