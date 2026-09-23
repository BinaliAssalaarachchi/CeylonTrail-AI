"""
CeylonTrail AI - Booking & Action Agent Schemas
Defines structured schemas for booking actions, itinerary items, and validation responses.
"""

from dataclasses import dataclass, field, asdict
from datetime import datetime
from enum import Enum
from typing import List, Optional, Dict, Any


class BookingActionType(str, Enum):
    CREATE_BOOKING_REQUEST = "CREATE_BOOKING_REQUEST"
    CANCEL_BOOKING_REQUEST = "CANCEL_BOOKING_REQUEST"
    REJECT_BOOKING_REQUEST = "REJECT_BOOKING_REQUEST"


class ItemType(str, Enum):
    ATTRACTION = "Attraction"
    ACCOMMODATION = "Accommodation"
    ACTIVITY = "Activity"
    TRANSPORT = "Transport"


@dataclass
class ItineraryActivitySlot:
    item_type: str
    target_id: str
    item_name: str
    quantity: int
    unit_price: float
    start_time: datetime
    end_time: datetime
    notes: Optional[str] = None


@dataclass
class TouristTripContext:
    tourist_id: str
    trip_id: str
    budget_limit: float
    party_size: int
    preferred_currency: str = "USD"


@dataclass
class BookingValidationIssue:
    code: str
    message: str
    target_id: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        return asdict(self)


@dataclass
class BookingActionPayload:
    action_type: BookingActionType
    trip_id: str
    tourist_id: str
    items: List[Dict[str, Any]]
    total_amount: float
    requires_approval: bool
    validation_issues: List[BookingValidationIssue] = field(default_factory=list)
    metadata: Dict[str, Any] = field(default_factory=dict)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "action_type": self.action_type.value,
            "trip_id": self.trip_id,
            "tourist_id": self.tourist_id,
            "items": self.items,
            "total_amount": self.total_amount,
            "requires_approval": self.requires_approval,
            "validation_issues": [issue.to_dict() for issue in self.validation_issues],
            "metadata": self.metadata,
        }
