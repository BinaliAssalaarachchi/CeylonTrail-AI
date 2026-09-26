from fastapi.testclient import TestClient

from destination.api import app


client = TestClient(app)


def test_health():
    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {
        "status": "ok",
        "service": "destination",
    }


def test_recommend_endpoint():
    response = client.post(
        "/destination/recommend",
        json={
            "request": {
                "district": "Kandy",
                "interests": ["Culture"],
                "categoryIds": [],
                "maxBudget": 1000,
                "date": "2026-10-01",
                "limit": 10,
            },
            "trustedAttractions": [
                {
                    "attractionId": "33333333-3333-3333-3333-333333333333",
                    "name": "Temple",
                    "district": "Kandy",
                    "categoryId": "44444444-4444-4444-4444-444444444444",
                    "category": "Culture",
                    "price": 500,
                    "status": "Approved",
                    "isActive": True,
                    "schedules": [],
                    "experienceSlots": [
                        {
                            "date": "2026-10-01",
                            "startTime": "09:00:00",
                            "endTime": "11:00:00",
                            "capacity": 20,
                            "availableCapacity": 10,
                        }
                    ],
                }
            ],
        },
    )

    assert response.status_code == 200

    body = response.json()

    assert body["status"] == "Success"
    assert len(body["candidates"]) == 1
    assert (
        body["candidates"][0]["attractionId"]
        == "33333333-3333-3333-3333-333333333333"
    )