"""HTTP boundary tests for the internal Travel Intelligence service."""

from uuid import uuid4
from unittest.mock import patch

from fastapi.testclient import TestClient

import travel_intelligence.api as travel_api
from travel_intelligence.agent import DeterministicExecutionError, TravelIntelligenceAgent

app = travel_api.app


client = TestClient(app)


def valid_payload():
    return {
        "validationResultId": str(uuid4()),
        "tripReference": "trip-001",
        "overallStatus": "Valid",
        "riskLevel": "Low",
        "isFeasible": True,
        "totalIssueCount": 0,
        "blockingIssueCount": 0,
        "issues": [],
    }


def test_health_endpoint():
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json()["status"] == "ok"


def test_analyze_valid_request_uses_structured_output_with_provider_fallback():
    class FailingProvider:
        name = "test-provider"
        model = "test-model"

        def recommend(self, validation, system_policy):
            raise RuntimeError("provider unavailable")

    with patch.object(
        travel_api,
        "agent",
        TravelIntelligenceAgent(FailingProvider()),
    ):
        response = client.post("/travel-intelligence/analyze", json=valid_payload())

    assert response.status_code == 200
    body = response.json()
    assert body["recommendedAction"] == "Proceed"
    assert body["isFeasible"] is True
    assert body["execution"]["usedFallback"] is True


def test_analyze_rejects_invalid_schema():
    payload = valid_payload()
    payload.pop("riskLevel")
    response = client.post("/travel-intelligence/analyze", json=payload)
    assert response.status_code == 422


def test_analyze_critical_validation_is_safe():
    payload = valid_payload()
    payload.update(
        {
            "overallStatus": "Invalid",
            "riskLevel": "Critical",
            "isFeasible": False,
            "totalIssueCount": 1,
            "blockingIssueCount": 1,
            "issues": [
                {
                    "issueType": "TravelAlert",
                    "severity": "Critical",
                    "ruleCode": "TRAVEL_ALERT_AFFECTS_ITINERARY",
                    "message": "Critical alert",
                    "isBlocking": True,
                    "relatedItemReference": "item-1",
                }
            ],
        }
    )
    response = client.post("/travel-intelligence/analyze", json=payload)
    assert response.status_code == 200
    body = response.json()
    assert body["recommendedAction"] == "Reschedule"
    assert body["requiresHumanApproval"] is True
    assert body["recommendedAction"] != "Proceed"


def test_analyze_deterministic_execution_failure_returns_safe_503():
    with patch.object(
        travel_api.agent,
        "analyze",
        side_effect=DeterministicExecutionError("internal sensitive detail"),
    ):
        response = client.post("/travel-intelligence/analyze", json=valid_payload())

    assert response.status_code == 503
    assert response.json() == {
        "detail": "Travel Intelligence analysis is temporarily unavailable."
    }
    assert "internal sensitive detail" not in response.text
