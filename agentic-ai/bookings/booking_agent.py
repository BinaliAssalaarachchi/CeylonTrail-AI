"""
CeylonTrail AI - Booking & Action Agent Implementation
Synthesizes structured booking actions from approved itineraries and verifies constraints.
"""

from datetime import datetime, timezone
from typing import List, Optional
from .schemas import (
    BookingActionType,
    BookingActionPayload,
    BookingValidationIssue,
    ItineraryActivitySlot,
    TouristTripContext,
)


class BookingActionAgent:
    """
    Autonomous Booking & Action Agent responsible for translating itinerary activities
    into validated, structured booking action payloads for the ASP.NET Core API.
    """

    def __init__(self, max_party_size: int = 20):
        self.max_party_size = max_party_size

    def check_schedule_overlaps(
        self, slots: List[ItineraryActivitySlot]
    ) -> List[BookingValidationIssue]:
        """Detects if any two activity slots overlap in time."""
        issues: List[BookingValidationIssue] = []
        sorted_slots = sorted(slots, key=lambda s: s.start_time)

        for i in range(len(sorted_slots)):
            for j in range(i + 1, len(sorted_slots)):
                slot_a = sorted_slots[i]
                slot_b = sorted_slots[j]

                # If start of B is before end of A, conflict exists
                if slot_b.start_time < slot_a.end_time:
                    issues.append(
                        BookingValidationIssue(
                            code="SCHEDULE_OVERLAP",
                            message=(
                                f"Schedule conflict detected between '{slot_a.item_name}' "
                                f"({slot_a.start_time.strftime('%H:%M')} - {slot_a.end_time.strftime('%H:%M')}) "
                                f"and '{slot_b.item_name}' "
                                f"({slot_b.start_time.strftime('%H:%M')} - {slot_b.end_time.strftime('%H:%M')})."
                            ),
                            target_id=slot_b.target_id,
                        )
                    )
        return issues

    def validate_budget(
        self, total_amount: float, budget_limit: float
    ) -> Optional[BookingValidationIssue]:
        """Validates that proposed total does not exceed the tourist's budget constraint."""
        if total_amount > budget_limit:
            return BookingValidationIssue(
                code="BUDGET_EXCEEDED",
                message=(
                    f"Total booking amount (${total_amount:.2f}) exceeds the tourist's "
                    f"budget limit (${budget_limit:.2f})."
                ),
            )
        return None

    def validate_party_size(
        self, slots: List[ItineraryActivitySlot], party_size: int
    ) -> List[BookingValidationIssue]:
        """Validates party size and item quantities."""
        issues: List[BookingValidationIssue] = []
        if party_size <= 0:
            issues.append(
                BookingValidationIssue(
                    code="INVALID_PARTY_SIZE",
                    message="Party size must be at least 1 person.",
                )
            )
        if party_size > self.max_party_size:
            issues.append(
                BookingValidationIssue(
                    code="PARTY_SIZE_EXCEEDED",
                    message=f"Party size ({party_size}) exceeds the maximum allowed ({self.max_party_size}).",
                )
            )

        for slot in slots:
            if slot.quantity <= 0:
                issues.append(
                    BookingValidationIssue(
                        code="INVALID_QUANTITY",
                        message=f"Quantity for '{slot.item_name}' must be greater than 0.",
                        target_id=slot.target_id,
                    )
                )
            if slot.unit_price < 0:
                issues.append(
                    BookingValidationIssue(
                        code="INVALID_PRICE",
                        message=f"Unit price for '{slot.item_name}' cannot be negative.",
                        target_id=slot.target_id,
                    )
                )
        return issues

    def synthesize_booking_action(
        self,
        context: TouristTripContext,
        slots: List[ItineraryActivitySlot],
    ) -> BookingActionPayload:
        """
        Executes the agent reasoning pipeline:
        1. Calculates item totals.
        2. Executes deterministic validation tools (overlaps, budget, capacity).
        3. Formats payload matching ASP.NET Core API DTO schema.
        4. Sets human-in-the-loop requires_approval flag.
        """
        issues: List[BookingValidationIssue] = []

        if not slots:
            issues.append(
                BookingValidationIssue(
                    code="EMPTY_ITINERARY",
                    message="Cannot generate booking action for an empty itinerary.",
                )
            )

        # 1. Schedule overlap validation
        issues.extend(self.check_schedule_overlaps(slots))

        # 2. Party & item validation
        issues.extend(self.validate_party_size(slots, context.party_size))

        # 3. Calculate total
        total_amount = sum(slot.quantity * slot.unit_price for slot in slots)

        # 4. Budget check
        budget_issue = self.validate_budget(total_amount, context.budget_limit)
        if budget_issue:
            issues.append(budget_issue)

        # 5. Format items matching ASP.NET Core `BookingItemRequest` DTO
        formatted_items = [
            {
                "itemType": slot.item_type,
                "targetId": slot.target_id,
                "itemName": slot.item_name,
                "quantity": slot.quantity,
                "unitPrice": slot.unit_price,
            }
            for slot in slots
        ]

        # Determine human-in-the-loop approval:
        # All booking actions require tourist approval before hitting the authoritative API
        requires_approval = True

        return BookingActionPayload(
            action_type=BookingActionType.CREATE_BOOKING_REQUEST,
            trip_id=context.trip_id,
            tourist_id=context.tourist_id,
            items=formatted_items,
            total_amount=round(total_amount, 2),
            requires_approval=requires_approval,
            validation_issues=issues,
            metadata={
                "item_count": len(slots),
                "generated_at": datetime.now(timezone.utc).isoformat(),
                "agent_name": "BookingActionAgent_v1",
                "valid": len(issues) == 0,
            },
        )
