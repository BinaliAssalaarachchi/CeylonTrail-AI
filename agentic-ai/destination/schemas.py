"""Strict request and response contracts for the Destination Agent."""

from datetime import date
from decimal import Decimal
from typing import List, Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field, model_validator


class DestinationActivityRequirement(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    reference: str = Field(min_length=1, max_length=100)
    activity_type: Optional[str] = Field(default=None, alias="activityType", max_length=100)
    preferred_district: Optional[str] = Field(default=None, alias="preferredDistrict", max_length=100)
    preferred_categories: List[str] = Field(default_factory=list, alias="preferredCategories", max_length=10)
    max_cost: Optional[Decimal] = Field(default=None, alias="maxCost", ge=0)


class DestinationCandidateAttraction(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    attraction_id: UUID = Field(alias="attractionId")
    name: str = Field(min_length=1, max_length=200)
    category: Optional[str] = Field(default=None, max_length=100)
    district: Optional[str] = Field(default=None, max_length=100)
    description_summary: Optional[str] = Field(default=None, alias="descriptionSummary", max_length=500)
    price: Decimal = Field(ge=0)
    opening_information: Optional[str] = Field(default=None, alias="openingInformation", max_length=500)


class DestinationInput(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    workflow_id: UUID = Field(alias="workflowId")
    trip_id: UUID = Field(alias="tripId")
    start_date: date = Field(alias="startDate")
    end_date: date = Field(alias="endDate")
    duration: int = Field(ge=1, le=366)
    budget: Decimal = Field(ge=0)
    interests: List[str] = Field(default_factory=list, max_length=30)
    preferred_regions: List[str] = Field(default_factory=list, alias="preferredRegions", max_length=30)
    requirements: List[DestinationActivityRequirement] = Field(default_factory=list, max_length=100)
    candidate_attractions: List[DestinationCandidateAttraction] = Field(
        default_factory=list, alias="candidateAttractions", max_length=500
    )

    @model_validator(mode="after")
    def validate_trip_context(self):
        if self.end_date < self.start_date:
            raise ValueError("endDate must be on or after startDate")
        if self.duration != (self.end_date - self.start_date).days + 1:
            raise ValueError("duration must match the inclusive trip date range")
        candidate_ids = [candidate.attraction_id for candidate in self.candidate_attractions]
        if len(candidate_ids) != len(set(candidate_ids)):
            raise ValueError("candidateAttractions must not contain duplicate IDs")
        requirement_refs = [requirement.reference for requirement in self.requirements]
        if len(requirement_refs) != len(set(requirement_refs)):
            raise ValueError("requirements must not contain duplicate references")
        return self


class DestinationSelection(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    requirement_reference: str = Field(alias="requirementReference", min_length=1, max_length=100)
    attraction_id: UUID = Field(alias="attractionId")
    fit_reasons: List[str] = Field(alias="fitReasons", min_length=1, max_length=6)
    explanation: str = Field(min_length=1, max_length=300)


class DestinationOutput(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    workflow_id: UUID = Field(alias="workflowId")
    selections: List[DestinationSelection] = Field(default_factory=list, max_length=100)
    unmatched_requirements: List[str] = Field(
        default_factory=list, alias="unmatchedRequirements", max_length=100
    )
    status: str = Field(pattern=r"^(Selected|NoMatch)$")
    summary: str = Field(min_length=1, max_length=500)

