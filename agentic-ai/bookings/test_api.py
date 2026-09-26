from datetime import datetime, timedelta, timezone
from uuid import uuid4

from fastapi.testclient import TestClient

from bookings.api import app


client = TestClient(app)


def test_health():
    response = client.get("/health")
    assert response.status_code == 200
    assert response.json() == {"status": "ok", "service": "booking-action"}


def test_prepare_endpoint_is_proposal_only():
    attraction_id = str(uuid4())
    start = datetime.now(timezone.utc) + timedelta(days=1)
    response = client.post("/booking/prepare", json={
        "workflowId": str(uuid4()),
        "tripId": str(uuid4()),
        "guestCount": 2,
        "selectedAttractionIds": [attraction_id],
        "trustedAvailabilitySlots": [{
            "availabilitySlotId": str(uuid4()),
            "attractionId": attraction_id,
            "attractionName": "Temple",
            "startTime": start.isoformat(),
            "endTime": (start + timedelta(hours=2)).isoformat(),
            "pricePerPerson": 25,
            "maxCapacity": 10,
            "bookedCapacity": 0,
            "availableCapacity": 10,
            "isActive": True,
            "isApproved": True,
        }],
    })
    assert response.status_code == 200
    assert response.json()["status"] == "Prepared"
    assert response.json()["requiresApproval"] is True

