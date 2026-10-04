"""Fixed proposal-only Booking Action operations over trusted slot snapshots."""

from dataclasses import dataclass
from datetime import datetime, timezone
from decimal import Decimal
from typing import Iterable, Optional

from .schemas import (
    BookingActionExecutionRequest,
    BookingActionIssue,
    BookingActionProposal,
    BookingAvailabilitySnapshot,
)


@dataclass(frozen=True)
class BookingToolResult:
    tool: str
    purpose: str
    result_summary: str
    slots: tuple[BookingAvailabilitySnapshot, ...] = ()
    costs: dict[str, Decimal] | None = None
    proposals: tuple[BookingActionProposal, ...] = ()
    issues: tuple[BookingActionIssue, ...] = ()


class BookingProposalTools:
    """Allow-listed, side-effect-free operations for proposal preparation."""

    @staticmethod
    def inspect_availability(request: BookingActionExecutionRequest) -> BookingToolResult:
        selected = set(request.selected_attraction_ids)
        slots = tuple(slot for slot in request.trusted_availability_slots if slot.attraction_id in selected)
        return BookingToolResult(
            "inspect_availability",
            "Inspect only the ASP.NET-supplied slots for selected attractions.",
            f"Found {len(slots)} trusted slot(s) for {len(selected)} selected attraction(s).",
            slots=slots,
        )

    @staticmethod
    def check_capacity(request: BookingActionExecutionRequest, slots: Iterable[BookingAvailabilitySnapshot]) -> BookingToolResult:
        eligible: list[BookingAvailabilitySnapshot] = []
        issues: list[BookingActionIssue] = []
        for slot in slots:
            if slot.available_capacity < request.guest_count:
                issues.append(BookingActionIssue(code="InsufficientCapacity", message="Slot does not have enough remaining capacity.", availabilitySlotId=slot.availability_slot_id))
            else:
                eligible.append(slot)
        return BookingToolResult(
            "check_capacity",
            "Compare requested guests with trusted remaining slot capacity.",
            f"{len(eligible)} slot(s) have capacity for {request.guest_count} guest(s); {len(issues)} rejected.",
            slots=tuple(eligible),
            issues=tuple(issues),
        )

    @staticmethod
    def check_trip_dates(request: BookingActionExecutionRequest, slots: Iterable[BookingAvailabilitySnapshot], now: Optional[datetime] = None) -> BookingToolResult:
        current = now or datetime.now(timezone.utc)
        eligible: list[BookingAvailabilitySnapshot] = []
        issues: list[BookingActionIssue] = []
        for slot in slots:
            if not slot.is_active or not slot.is_approved:
                issues.append(BookingActionIssue(code="InvalidSlot", message="Slot is not publicly eligible.", availabilitySlotId=slot.availability_slot_id))
            elif slot.start_time <= current or slot.end_time <= current:
                issues.append(BookingActionIssue(code="ExpiredSlot", message="Slot is not a future availability window.", availabilitySlotId=slot.availability_slot_id))
            elif ((request.start_date and slot.start_time.date() < request.start_date) or
                  (request.end_date and slot.end_time.date() > request.end_date)):
                issues.append(BookingActionIssue(code="OutsideTripDates", message="Slot is outside the supplied trip dates.", availabilitySlotId=slot.availability_slot_id))
            else:
                eligible.append(slot)
        return BookingToolResult(
            "check_trip_dates",
            "Check public eligibility, future timing, and supplied trip-date boundaries.",
            f"{len(eligible)} slot(s) remain within valid trip dates; {len(issues)} rejected.",
            slots=tuple(eligible),
            issues=tuple(issues),
        )

    @staticmethod
    def calculate_proposal_cost(request: BookingActionExecutionRequest, slots: Iterable[BookingAvailabilitySnapshot]) -> BookingToolResult:
        source = tuple(slots)
        costs = {
            str(slot.availability_slot_id): slot.price_per_person * request.guest_count
            for slot in source
        }
        total = sum(costs.values(), Decimal("0"))
        return BookingToolResult(
            "calculate_proposal_cost",
            "Calculate a non-authoritative proposal cost from trusted snapshot prices and guest count.",
            f"Calculated LKR {total} proposal cost across {len(source)} eligible slot(s); ASP.NET remains authoritative.",
            slots=source,
            costs=costs,
        )

    @staticmethod
    def check_remaining_budget(request: BookingActionExecutionRequest, slots: Iterable[BookingAvailabilitySnapshot], costs: dict[str, Decimal]) -> BookingToolResult:
        ordered = tuple(sorted(slots, key=lambda slot: (slot.start_time, slot.price_per_person, str(slot.availability_slot_id))))
        eligible: list[BookingAvailabilitySnapshot] = []
        issues: list[BookingActionIssue] = []
        total = Decimal("0")
        for slot in ordered:
            subtotal = costs[str(slot.availability_slot_id)]
            if request.remaining_budget is not None and total + subtotal > request.remaining_budget:
                issues.append(BookingActionIssue(code="BudgetExceeded", message="Slot proposal would exceed the remaining budget.", availabilitySlotId=slot.availability_slot_id))
            else:
                eligible.append(slot)
                total += subtotal
        budget_text = f" within LKR {request.remaining_budget}" if request.remaining_budget is not None else " without a remaining budget cap"
        return BookingToolResult(
            "check_remaining_budget",
            "Keep proposals whose calculated snapshot cost fits the remaining budget.",
            f"{len(eligible)} slot(s) fit{budget_text}; {len(issues)} rejected.",
            slots=tuple(eligible),
            costs=costs,
            issues=tuple(issues),
        )

    @staticmethod
    def build_booking_proposal(request: BookingActionExecutionRequest, slots: Iterable[BookingAvailabilitySnapshot], costs: dict[str, Decimal]) -> BookingToolResult:
        proposals = tuple(
            BookingActionProposal(
                attractionId=slot.attraction_id,
                availabilitySlotId=slot.availability_slot_id,
                guestCount=request.guest_count,
                unitPrice=slot.price_per_person,
                totalPrice=costs[str(slot.availability_slot_id)],
                startTime=slot.start_time,
                endTime=slot.end_time,
                reason="Eligible future snapshot slot with sufficient capacity and proposal pricing; final authority remains ASP.NET.",
            )
            for slot in slots
        )
        return BookingToolResult(
            "build_booking_proposal",
            "Build proposal records without creating or confirming a booking.",
            f"Built {len(proposals)} proposal record(s); no booking side effect was performed.",
            slots=tuple(slots),
            costs=costs,
            proposals=proposals,
        )

    @staticmethod
    def explain_ineligible_option(issues: Iterable[BookingActionIssue]) -> BookingToolResult:
        source = tuple(issues)
        summary = f"Recorded {len(source)} ineligible option explanation(s)."
        return BookingToolResult(
            "explain_ineligible_option",
            "Explain why trusted snapshot options cannot currently receive a proposal.",
            summary,
            issues=source,
        )

