from datetime import datetime, timedelta, timezone
from decimal import Decimal
from uuid import uuid4

import pytest
from pydantic import ValidationError

from bookings.booking_agent import BookingProposalAgent
from bookings.schemas import BookingActionExecutionRequest


def slot(*, slot_id=None, attraction_id=None, price="20", available=5, start=None, active=True, approved=True):
    start = start or datetime.now(timezone.utc) + timedelta(days=1)
    return {
        "availabilitySlotId": str(slot_id or uuid4()),
        "attractionId": str(attraction_id or uuid4()),
        "attractionName": "Trusted attraction",
        "startTime": start.isoformat(),
        "endTime": (start + timedelta(hours=2)).isoformat(),
        "pricePerPerson": price,
        "maxCapacity": 10,
        "bookedCapacity": 10 - available,
        "availableCapacity": available,
        "isActive": active,
        "isApproved": approved,
    }


def request(slots, *, guest_count=2, budget=None, start_date=None, end_date=None, attraction_ids=None):
    ids = attraction_ids or [item["attractionId"] for item in slots]
    return BookingActionExecutionRequest.model_validate({
        "workflowId": str(uuid4()),
        "tripId": str(uuid4()),
        "guestCount": guest_count,
        "remainingBudget": budget,
        "selectedAttractionIds": ids,
        "startDate": start_date,
        "endDate": end_date,
        "trustedAvailabilitySlots": slots,
    })


def test_valid_proposal_uses_authoritative_price():
    parsed = request([slot(price="20")])
    result = BookingProposalAgent().prepare(parsed)
    assert result.status == "Prepared"
    assert result.proposals[0].unit_price == Decimal("20")
    assert result.proposals[0].total_price == Decimal("40")
    assert result.requires_approval is True


def test_insufficient_capacity_produces_safe_no_proposal():
    result = BookingProposalAgent().prepare(request([slot(available=1)]))
    assert result.status == "NoEligibleProposal"
    assert any(issue.code == "InsufficientCapacity" for issue in result.issues)


def test_budget_exceeded_produces_safe_no_proposal():
    result = BookingProposalAgent().prepare(request([slot(price="20")], budget="10"))
    assert result.status == "NoEligibleProposal"
    assert any(issue.code == "BudgetExceeded" for issue in result.issues)


def test_outside_trip_dates_is_rejected():
    start = datetime(2026, 10, 5, 9, tzinfo=timezone.utc)
    result = BookingProposalAgent().prepare(request([slot(start=start)], start_date="2026-10-01", end_date="2026-10-02"))
    assert result.status == "NoEligibleProposal"
    assert any(issue.code == "OutsideTripDates" for issue in result.issues)


def test_expired_slot_is_rejected():
    start = datetime.now(timezone.utc) - timedelta(hours=3)
    result = BookingProposalAgent().prepare(request([slot(start=start)]))
    assert result.status == "NoEligibleProposal"
    assert any(issue.code == "ExpiredSlot" for issue in result.issues)


def test_duplicate_slot_records_are_rejected_by_schema():
    item = slot()
    with pytest.raises(ValidationError, match="duplicate"):
        request([item, item])


def test_deterministic_selection_prefers_earliest_then_price_then_id():
    first = slot(price="30", start=datetime.now(timezone.utc) + timedelta(days=1))
    second = slot(price="20", start=datetime.now(timezone.utc) + timedelta(days=1))
    first["attractionId"] = str(uuid4())
    second["attractionId"] = str(uuid4())
    parsed = request([first, second])
    assert BookingProposalAgent().prepare(parsed) == BookingProposalAgent().prepare(parsed)


def test_no_selected_attraction_match_is_safe():
    item = slot()
    result = BookingProposalAgent().prepare(request([item], attraction_ids=[str(uuid4())]))
    assert result.status == "NoEligibleProposal"
    assert any(issue.code == "NoAvailability" for issue in result.issues)


def test_malformed_input_is_rejected():
    with pytest.raises(ValidationError):
        BookingActionExecutionRequest.model_validate({"guestCount": 0, "trustedAvailabilitySlots": []})


def test_extra_fields_are_rejected():
    item = slot()
    item["secret"] = "do-not-accept"
    with pytest.raises(ValidationError):
        request([item])


def test_ineligible_records_are_never_proposed():
    result = BookingProposalAgent().prepare(request([slot(active=False), slot(approved=False)]))
    assert result.status == "NoEligibleProposal"
    assert result.proposals == []
