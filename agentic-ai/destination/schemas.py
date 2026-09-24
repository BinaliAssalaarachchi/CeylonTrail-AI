"""Strict contracts for the grounded Destination Agent."""

from datetime import date as Date, time
from decimal import Decimal
from typing import List, Optional

from pydantic import BaseModel, ConfigDict, Field, field_validator


class DestinationRequest(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    district: Optional[str] = Field(default=None, max_length=100)
    interests: List[str] = Field(default_factory=list, max_length=20)
    category_ids: List[str] = Field(default_factory=list, alias="categoryIds", max_length=20)
    max_budget: Optional[Decimal] = Field(default=None, alias="maxBudget", ge=0)
    date: Optional[Date] = None
    limit: int = Field(default=10, ge=1, le=50)

    @field_validator("district")
    @classmethod
    def validate_district(cls, value: Optional[str]) -> Optional[str]:
        return value.strip() if value and value.strip() else None

    @field_validator("interests", "category_ids")
    @classmethod
    def validate_values(cls, values: List[str]) -> List[str]:
        cleaned = [value.strip() for value in values if value and value.strip()]
        if len(cleaned) != len(set(cleaned)):
            raise ValueError("constraint values must not contain duplicates")
        return cleaned


class ScheduleRecord(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    day_of_week: str = Field(alias="dayOfWeek", min_length=1, max_length=20)
    opening_time: Optional[time] = Field(default=None, alias="openingTime")
    closing_time: Optional[time] = Field(default=None, alias="closingTime")
    is_closed: bool = Field(alias="isClosed")


class AvailabilityRecord(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    date: Date
    start_time: time = Field(alias="startTime")
    end_time: time = Field(alias="endTime")
    capacity: int = Field(ge=0)
    available_capacity: int = Field(alias="availableCapacity", ge=0)


class TrustedAttraction(BaseModel):
    """Attraction data supplied by the authoritative ASP.NET service."""

    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    attraction_id: str = Field(alias="attractionId", min_length=1, max_length=100)
    name: str = Field(min_length=1, max_length=200)
    district: str = Field(min_length=1, max_length=100)
    category_id: str = Field(alias="categoryId", min_length=1, max_length=100)
    category: str = Field(min_length=1, max_length=100)
    price: Decimal = Field(ge=0)
    status: str = Field(min_length=1, max_length=50)
    is_active: bool = Field(alias="isActive")
    schedules: List[ScheduleRecord] = Field(default_factory=list, max_length=100)
    experience_slots: List[AvailabilityRecord] = Field(default_factory=list, alias="experienceSlots", max_length=500)


class DestinationCandidate(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    attraction_id: str = Field(alias="attractionId", min_length=1, max_length=100)
    name: str = Field(min_length=1, max_length=200)
    district: str = Field(min_length=1, max_length=100)
    category_id: str = Field(alias="categoryId", min_length=1, max_length=100)
    category: str = Field(min_length=1, max_length=100)
    price: Decimal = Field(ge=0)
    opening_hours: List[ScheduleRecord] = Field(default_factory=list, alias="openingHours", max_length=100)
    availability: List[AvailabilityRecord] = Field(default_factory=list, max_length=500)
    match_reasons: List[str] = Field(default_factory=list, alias="matchReasons", max_length=10)
    score: int = Field(ge=0)


class DestinationOutput(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    candidates: List[DestinationCandidate] = Field(default_factory=list, max_length=50)
    status: str = Field(pattern=r"^(Success|NoResults)$")
    message: Optional[str] = Field(default=None, max_length=500)


class DestinationExecutionRequest(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    request: DestinationRequest
    trusted_attractions: List[TrustedAttraction] = Field(alias="trustedAttractions", max_length=500)
