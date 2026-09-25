from fastapi.testclient import TestClient

from destination.api import app


client = TestClient(app)


def test_health():
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok", "service": "destination"}


def test_select_endpoint():
    response = client.post("/destination/select", json={
        "workflowId": "11111111-1111-1111-1111-111111111111",
        "tripId": "22222222-2222-2222-2222-222222222222",
        "startDate": "2026-10-01",
        "endDate": "2026-10-01",
        "duration": 1,
        "budget": 1000,
        "requirements": [{"reference": "slot-1", "activityType": "Culture"}],
        "candidateAttractions": [{
            "attractionId": "33333333-3333-3333-3333-333333333333",
            "name": "Temple",
            "category": "Culture",
            "district": "Kandy",
            "price": 500,
        }],
    })
    assert response.status_code == 200
    assert response.json()["selections"][0]["attractionId"] == "33333333-3333-3333-3333-333333333333"

