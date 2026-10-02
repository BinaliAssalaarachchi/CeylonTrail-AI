"""
CeylonTrail AI - Booking & Action Agent Schemas
Defines structured schemas for booking actions, itinerary items, and validation responses.
"""

from dataclasses import dataclass, field, asdict
from datetime import date, datetime, timezone
from decimal import Decimal
from enum import Enum
from typing import List, Optional, Dict, Any

from pydantic import BaseModel, ConfigDict, Field, model_validator
from uuid import UUID

from agent_trace import AgentExecutionTrace


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


class BookingAvailabilitySnapshot(BaseModel):
    """Authoritative M3 AvailabilitySlot data supplied by ASP.NET."""

    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    availability_slot_id: UUID = Field(alias="availabilitySlotId")
    attraction_id: UUID = Field(alias="attractionId")
    attraction_name: str = Field(alias="attractionName", min_length=1, max_length=200)
    start_time: datetime = Field(alias="startTime")
    end_time: datetime = Field(alias="endTime")
    price_per_person: Decimal = Field(alias="pricePerPerson", ge=0)
    max_capacity: int = Field(alias="maxCapacity", ge=1)
    booked_capacity: int = Field(alias="bookedCapacity", ge=0)
    available_capacity: int = Field(alias="availableCapacity", ge=0)
    is_active: bool = Field(alias="isActive")
    is_approved: bool = Field(alias="isApproved")

    @model_validator(mode="after")
    def validate_slot(self):
        if self.start_time.tzinfo is None or self.end_time.tzinfo is None:
            raise ValueError("availability slot times must include a timezone")
        if self.end_time <= self.start_time:
            raise ValueError("availability slot endTime must be after startTime")
        if self.booked_capacity > self.max_capacity:
            raise ValueError("bookedCapacity must not exceed maxCapacity")
        if self.available_capacity != self.max_capacity - self.booked_capacity:
            raise ValueError("availableCapacity must match maxCapacity minus bookedCapacity")
        return self


class BookingActionExecutionRequest(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    workflow_id: UUID = Field(alias="workflowId")
    trip_id: UUID = Field(alias="tripId")
    guest_count: int = Field(alias="guestCount", ge=1, le=100)
    remaining_budget: Optional[Decimal] = Field(default=None, alias="remainingBudget", ge=0)
    selected_attraction_ids: List[UUID] = Field(alias="selectedAttractionIds", min_length=1, max_length=100)
    start_date: Optional[date] = Field(default=None, alias="startDate")
    end_date: Optional[date] = Field(default=None, alias="endDate")
    trusted_availability_slots: List[BookingAvailabilitySnapshot] = Field(
        alias="trustedAvailabilitySlots", max_length=500
    )

    @model_validator(mode="after")
    def validate_request(self):
        if len(self.selected_attraction_ids) != len(set(self.selected_attraction_ids)):
            raise ValueError("selectedAttractionIds must not contain duplicates")
        if self.start_date and self.end_date and self.end_date < self.start_date:
            raise ValueError("endDate must be on or after startDate")
        slot_ids = [slot.availability_slot_id for slot in self.trusted_availability_slots]
        if len(slot_ids) != len(set(slot_ids)):
            raise ValueError("trustedAvailabilitySlots must not contain duplicate IDs")
        return self


class BookingActionIssue(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    code: str = Field(min_length=1, max_length=40)
    message: str = Field(min_length=1, max_length=300)
    availability_slot_id: Optional[UUID] = Field(default=None, alias="availabilitySlotId")


class BookingActionProposal(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    attraction_id: UUID = Field(alias="attractionId")
    availability_slot_id: UUID = Field(alias="availabilitySlotId")
    guest_count: int = Field(alias="guestCount", ge=1, le=100)
    unit_price: Decimal = Field(alias="unitPrice", ge=0)
    total_price: Decimal = Field(alias="totalPrice", ge=0)
    start_time: datetime = Field(alias="startTime")
    end_time: datetime = Field(alias="endTime")
    reason: str = Field(min_length=1, max_length=300)

    @model_validator(mode="after")
    def validate_price_and_time(self):
        if self.total_price != self.unit_price * self.guest_count:
            raise ValueError("totalPrice must equal unitPrice multiplied by guestCount")
        if self.end_time <= self.start_time:
            raise ValueError("proposal endTime must be after startTime")
        return self


class BookingActionOutput(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    workflow_id: UUID = Field(alias="workflowId")
    trip_id: UUID = Field(alias="tripId")
    status: str = Field(pattern=r"^(Prepared|NoEligibleProposal)$")
    proposals: List[BookingActionProposal] = Field(default_factory=list, max_length=100)
    issues: List[BookingActionIssue] = Field(default_factory=list, max_length=100)
    requires_approval: bool = Field(alias="requiresApproval")
    summary: str = Field(min_length=1, max_length=500)
    trace: Optional[AgentExecutionTrace] = None
