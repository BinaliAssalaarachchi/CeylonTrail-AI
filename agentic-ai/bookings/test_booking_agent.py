"""
Unit tests for CeylonTrail AI Booking & Action Agent.
"""

import unittest
from datetime import datetime, timedelta, timezone
from bookings.schemas import (
    BookingActionType,
    ItineraryActivitySlot,
    TouristTripContext,
)
from bookings.booking_agent import BookingActionAgent


class TestBookingActionAgent(unittest.TestCase):

    def setUp(self):
        self.agent = BookingActionAgent(max_party_size=10)
        self.context = TouristTripContext(
            tourist_id="tourist-123",
            trip_id="trip-456",
            budget_limit=500.0,
            party_size=2,
        )

    def test_valid_booking_action_synthesis(self):
        base_time = datetime(2026, 10, 1, 9, 0, tzinfo=timezone.utc)
        slots = [
            ItineraryActivitySlot(
                item_type="Attraction",
                target_id="attraction-1",
                item_name="Sigiriya Rock Fortress",
                quantity=2,
                unit_price=30.0,
                start_time=base_time,
                end_time=base_time + timedelta(hours=3),
            ),
            ItineraryActivitySlot(
                item_type="Activity",
                target_id="activity-2",
                item_name="Dambulla Cave Tour",
                quantity=2,
                unit_price=20.0,
                start_time=base_time + timedelta(hours=4),
                end_time=base_time + timedelta(hours=6),
            ),
        ]

        payload = self.agent.synthesize_booking_action(self.context, slots)

        self.assertEqual(payload.action_type, BookingActionType.CREATE_BOOKING_REQUEST)
        self.assertEqual(payload.trip_id, "trip-456")
        self.assertEqual(payload.tourist_id, "tourist-123")
        self.assertEqual(len(payload.items), 2)
        self.assertEqual(payload.total_amount, 100.0)  # (2*30) + (2*20)
        self.assertTrue(payload.requires_approval)
        self.assertEqual(len(payload.validation_issues), 0)
        self.assertTrue(payload.metadata["valid"])

    def test_schedule_overlap_detection(self):
        base_time = datetime(2026, 10, 1, 9, 0, tzinfo=timezone.utc)
        slots = [
            ItineraryActivitySlot(
                item_type="Attraction",
                target_id="attraction-1",
                item_name="Temple of the Tooth",
                quantity=1,
                unit_price=15.0,
                start_time=base_time,
                end_time=base_time + timedelta(hours=2),
            ),
            ItineraryActivitySlot(
                item_type="Activity",
                target_id="activity-2",
                item_name="Kandy City Walk",
                quantity=1,
                unit_price=10.0,
                start_time=base_time + timedelta(hours=1),  # Overlaps!
                end_time=base_time + timedelta(hours=3),
            ),
        ]

        payload = self.agent.synthesize_booking_action(self.context, slots)

        self.assertFalse(payload.metadata["valid"])
        overlap_issues = [i for i in payload.validation_issues if i.code == "SCHEDULE_OVERLAP"]
        self.assertEqual(len(overlap_issues), 1)

    def test_budget_exceeded_detection(self):
        base_time = datetime(2026, 10, 1, 9, 0, tzinfo=timezone.utc)
        slots = [
            ItineraryActivitySlot(
                item_type="Accommodation",
                target_id="hotel-1",
                item_name="Luxury Hill Resort",
                quantity=1,
                unit_price=600.0,  # Exceeds $500 budget
                start_time=base_time,
                end_time=base_time + timedelta(days=1),
            )
        ]

        payload = self.agent.synthesize_booking_action(self.context, slots)

        self.assertFalse(payload.metadata["valid"])
        budget_issues = [i for i in payload.validation_issues if i.code == "BUDGET_EXCEEDED"]
        self.assertEqual(len(budget_issues), 1)

    def test_empty_itinerary_handling(self):
        payload = self.agent.synthesize_booking_action(self.context, [])
        self.assertFalse(payload.metadata["valid"])
        empty_issues = [i for i in payload.validation_issues if i.code == "EMPTY_ITINERARY"]
        self.assertEqual(len(empty_issues), 1)


if __name__ == "__main__":
    unittest.main()
