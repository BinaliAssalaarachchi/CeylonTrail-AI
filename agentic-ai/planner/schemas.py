"""Strict input and output contracts for the Planner Agent."""

from datetime import date, time
from decimal import Decimal
from typing import List, Optional

from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator


class PlannerPreference(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    type: str = Field(min_length=1, max_length=50)
    # TripPreference.Value is authoritative and supports the persisted
    # objective text up to 500 characters.
    value: str = Field(min_length=1, max_length=500)


class CandidateAttraction(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    id: str = Field(min_length=1, max_length=100)
    name: str = Field(min_length=1, max_length=200)
    category: Optional[str] = Field(default=None, max_length=100)
    region: Optional[str] = Field(default=None, max_length=100)
    price: Decimal = Field(ge=0)
    description: Optional[str] = Field(default=None, max_length=1000)
    opening_information: Optional[str] = Field(default=None, alias="openingInformation", max_length=500)


class PlannerInput(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    trip_id: str = Field(alias="tripId", min_length=1, max_length=100)
    start_date: date = Field(alias="startDate")
    end_date: date = Field(alias="endDate")
    duration: int = Field(ge=1, le=366)
    budget: Decimal = Field(ge=0)
    interests: List[str] = Field(default_factory=list, max_length=30)
    preferred_regions: List[str] = Field(default_factory=list, alias="preferredRegions", max_length=30)
    preferences: List[PlannerPreference] = Field(default_factory=list, max_length=30)
    candidate_attractions: List[CandidateAttraction] = Field(default_factory=list, alias="candidateAttractions", max_length=500)

    @model_validator(mode="after")
    def validate_dates_and_duration(self):
        expected_duration = (self.end_date - self.start_date).days + 1
        if self.end_date < self.start_date:
            raise ValueError("endDate must be on or after startDate")
        if self.duration != expected_duration:
            raise ValueError("duration must match the inclusive trip date range")
        return self


class PlannerItem(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    attraction_id: str = Field(alias="attractionId", min_length=1, max_length=100)
    start_time: time = Field(alias="startTime")
    end_time: time = Field(alias="endTime")
    estimated_cost: Decimal = Field(alias="estimatedCost", ge=0)
    notes: Optional[str] = Field(default=None, max_length=1000)

    @model_validator(mode="after")
    def validate_time_range(self):
        if self.end_time <= self.start_time:
            raise ValueError("endTime must be after startTime")
        return self


class PlannerDay(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    day_number: int = Field(alias="dayNumber", gt=0)
    date: date
    items: List[PlannerItem] = Field(default_factory=list, max_length=50)


class PlannerOutput(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    days: List[PlannerDay] = Field(default_factory=list, max_length=366)
    estimated_cost: Decimal = Field(alias="estimatedCost", ge=0)
    status: str = Field(pattern=r"^(Generated|NoPlan)$")
    message: Optional[str] = Field(default=None, max_length=500)


def validate_planner_output(output: PlannerOutput, request: PlannerInput) -> PlannerOutput:
    """Apply deterministic rules that require the request's trusted context."""

    candidate_ids = {candidate.id for candidate in request.candidate_attractions}
    if len(candidate_ids) != len(request.candidate_attractions):
        raise ValueError("candidateAttractions must not contain duplicate IDs")

    day_numbers = [day.day_number for day in output.days]
    if len(day_numbers) != len(set(day_numbers)):
        raise ValueError("dayNumbers must be unique")

    total = Decimal("0")
    scheduled_ids: set[str] = set()
    for day in output.days:
        if not request.start_date <= day.date <= request.end_date:
            raise ValueError("itinerary day dates must fall within the trip dates")
        previous_end: Optional[time] = None
        for item in sorted(day.items, key=lambda value: value.start_time):
            if item.attraction_id not in candidate_ids:
                raise ValueError("itinerary item references an unknown candidate attraction")
            if item.attraction_id in scheduled_ids:
                raise ValueError("an attraction may only be scheduled once")
            if previous_end is not None and item.start_time < previous_end:
                raise ValueError("itinerary items must not overlap")
            previous_end = item.end_time
            scheduled_ids.add(item.attraction_id)
            total += item.estimated_cost

    if total != output.estimated_cost:
        raise ValueError("estimatedCost must equal the sum of item costs")
    if total > request.budget:
        raise ValueError("estimatedCost must not exceed the trip budget")
    if output.status == "Generated" and not output.days:
        raise ValueError("a Generated itinerary must contain at least one day")
    if output.status == "NoPlan" and output.days:
        raise ValueError("a NoPlan result must not contain itinerary days")
    return output
