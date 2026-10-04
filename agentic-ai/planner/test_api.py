"""HTTP boundary tests for the dedicated Planner service."""

from fastapi.testclient import TestClient

import planner.api as planner_api
from planner.agent import PlannerAgent
from planner.providers import MissingPlannerProvider
from planner.providers import PlannerProviderError


client = TestClient(planner_api.app)


def valid_payload():
    return {
        "tripId": "trip-1", "startDate": "2026-10-10", "endDate": "2026-10-12", "duration": 3,
        "budget": 60000, "interests": ["culture"], "preferredRegions": ["Kandy"],
        "preferences": [{"type": "TravelStyle", "value": "Relaxed"}],
        "candidateAttractions": [
            {"id": "a1", "name": "Temple", "category": "Culture", "region": "Kandy", "price": 2500}
        ],
    }


def aspnet_generated_payload_with_long_objective():
    payload = valid_payload()
    payload["preferences"] = [{
        "type": "Objective",
        "value": (
            "Plan a one-day cultural heritage trip to Sigiriya on 3 October 2026 "
            "within LKR 60,000. Prioritize approved attractions and currently "
            "available bookable experiences. Include the Sigiriya Heritage Sunrise Trail where suitable."
        ),
    }]
    return payload


class FakeProvider:
    def generate(self, request, system_prompt):
        return {
            "days": [{"dayNumber": 1, "date": "2026-10-10", "items": [{
                "attractionId": "a1", "startTime": "09:00", "endTime": "10:00", "estimatedCost": 2500,
            }]}],
            "estimatedCost": 2500, "status": "Generated",
        }


def test_health_endpoint():
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok", "service": "planner"}


def test_generate_with_mocked_provider():
    original = planner_api.planner_agent
    planner_api.planner_agent = PlannerAgent(FakeProvider())
    try:
        response = client.post("/planner/generate", json=valid_payload())
    finally:
        planner_api.planner_agent = original
    assert response.status_code == 200
    assert response.json()["status"] == "Generated"
    assert [step["tool"] for step in response.json()["trace"]["steps"]] == [
        "inspect_trip_constraints",
        "inspect_candidate_attractions",
        "calculate_budget_usage",
        "check_schedule_conflicts",
        "validate_plan_constraints",
    ]


def test_deterministic_failure_returns_structured_safe_failure_trace():
    class InvalidProvider:
        def generate(self, request, system_prompt):
            invalid = FakeProvider().generate(request, system_prompt)
            invalid["days"][0]["items"][0]["attractionId"] = "unknown"
            return invalid

    original = planner_api.planner_agent
    planner_api.planner_agent = PlannerAgent(InvalidProvider(), max_retries=0)
    try:
        response = client.post("/planner/generate", json=valid_payload())
    finally:
        planner_api.planner_agent = original
    assert response.status_code == 422
    detail = response.json()["detail"]
    assert detail["safeFailure"] == "Itinerary item references an unknown candidate attraction."
    assert [step["tool"] for step in detail["trace"]["steps"]] == [
        "inspect_trip_constraints",
        "inspect_candidate_attractions",
        "calculate_budget_usage",
    ]


def test_actual_aspnet_payload_shape_accepts_500_character_objective():
    original = planner_api.planner_agent
    planner_api.planner_agent = PlannerAgent(FakeProvider())
    try:
        response = client.post(
            "/planner/generate",
            json=aspnet_generated_payload_with_long_objective(),
        )
    finally:
        planner_api.planner_agent = original
    assert response.status_code == 200
    assert response.json()["status"] == "Generated"


def test_missing_key_is_controlled_503():
    original = planner_api.planner_agent
    planner_api.planner_agent = PlannerAgent(MissingPlannerProvider())
    try:
        response = client.post("/planner/generate", json=valid_payload())
    finally:
        planner_api.planner_agent = original
    assert response.status_code == 503


def test_invalid_request_is_validation_error():
    payload = valid_payload()
    payload["endDate"] = "not-a-date"
    response = client.post("/planner/generate", json=payload)
    assert response.status_code == 422


def test_upstream_unavailable_is_not_misreported_as_bad_gateway():
    class UnavailableProvider:
        def generate(self, request, system_prompt):
            raise PlannerProviderError("upstream unavailable", retryable=False, status_code=503)

    original = planner_api.planner_agent
    planner_api.planner_agent = PlannerAgent(UnavailableProvider())
    try:
        response = client.post("/planner/generate", json=valid_payload())
    finally:
        planner_api.planner_agent = original
    assert response.status_code == 503
