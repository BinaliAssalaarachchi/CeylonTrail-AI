from datetime import date, time
from decimal import Decimal

import pytest
from pydantic import ValidationError

from .agent import DestinationAgent, validate_destination_output
from .schemas import DestinationExecutionRequest, DestinationOutput, TrustedAttraction


def attraction(id="a", *, district="Kandy", category="Culture", price="2000", approved=True, active=True, available=True):
    return TrustedAttraction(
        attractionId=id, name=f"Attraction {id}", district=district, categoryId=f"cat-{category.lower()}", category=category,
        price=price, status="Approved" if approved else "PendingApproval", isActive=active,
        schedules=[{"dayOfWeek": "Monday", "openingTime": "09:00:00", "closingTime": "17:00:00", "isClosed": False}],
        experienceSlots=[{"date": "2026-09-30", "startTime": "10:00:00", "endTime": "12:00:00", "capacity": 10, "availableCapacity": 5}] if available else [],
    )


def execute(request, attractions):
    return DestinationAgent().recommend(DestinationExecutionRequest(request=request, trustedAttractions=attractions))


def test_filters_district_category_budget_and_date():
    result = execute({"district": "Kandy", "interests": ["Culture"], "maxBudget": 3000, "date": "2026-09-30", "limit": 5}, [attraction(), attraction("b", district="Galle", category="Beach", price="1000")])
    assert [item.attraction_id for item in result.candidates] == ["a"]


def test_approved_active_only_and_no_results():
    result = execute({}, [attraction("pending", approved=False), attraction("inactive", active=False)])
    assert result.status == "NoResults" and result.candidates == []


def test_unknown_model_id_is_rejected():
    source = [attraction()]
    candidate = source[0]
    output = DestinationOutput(candidates=[{
        "attractionId": "unknown", "name": candidate.name, "district": candidate.district, "categoryId": candidate.category_id,
        "category": candidate.category, "price": candidate.price, "availability": [], "openingHours": [], "matchReasons": [], "score": 1
    }], status="Success")
    with pytest.raises(ValueError, match="unknown attraction ID"):
        validate_destination_output(output, DestinationExecutionRequest(request={}, trustedAttractions=source).request, source)


def test_invalid_input_is_rejected():
    with pytest.raises(ValidationError):
        DestinationExecutionRequest(request={"maxBudget": -1}, trustedAttractions=[])
    with pytest.raises(ValidationError):
        DestinationExecutionRequest(request={"limit": 51}, trustedAttractions=[])
    with pytest.raises(ValidationError):
        DestinationExecutionRequest(request={"date": "not-a-date"}, trustedAttractions=[])


def test_duplicate_output_is_rejected():
    source = [attraction()]
    result = execute({}, source)
    duplicate = DestinationOutput(candidates=[result.candidates[0], result.candidates[0]], status="Success")
    with pytest.raises(ValueError, match="duplicate"):
        validate_destination_output(duplicate, DestinationExecutionRequest(request={}, trustedAttractions=source).request, source)
