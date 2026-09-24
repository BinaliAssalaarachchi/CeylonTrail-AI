"""HTTP boundary tests for the dedicated Planner service."""

from fastapi.testclient import TestClient

import planner.api as planner_api
from planner.agent import PlannerAgent
from planner.providers import MissingPlannerProvider


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
