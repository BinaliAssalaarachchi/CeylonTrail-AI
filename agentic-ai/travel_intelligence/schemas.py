"""Strict schemas exchanged with the deterministic ASP.NET validation boundary."""

from enum import Enum
from typing import List, Optional
from uuid import UUID

from pydantic import BaseModel, ConfigDict, Field


class RiskLevel(str, Enum):
    LOW = "Low"
    MEDIUM = "Medium"
    HIGH = "High"
    CRITICAL = "Critical"


class ValidationOverallStatus(str, Enum):
    VALID = "Valid"
    WARNING = "Warning"
    INVALID = "Invalid"


class ValidationIssueType(str, Enum):
    SCHEDULE_CONFLICT = "ScheduleConflict"
    BUDGET_EXCEEDED = "BudgetExceeded"
    TRAVEL_ALERT = "TravelAlert"
    INVALID_TIME_RANGE = "InvalidTimeRange"
    AVAILABILITY = "Availability"
    GENERAL = "General"


class ValidationIssueInput(BaseModel):
    """A deterministic issue from ASP.NET; descriptive text is untrusted data."""

    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    issue_type: ValidationIssueType = Field(alias="issueType")
    severity: RiskLevel
    rule_code: str = Field(alias="ruleCode", min_length=1, max_length=100)
    message: str = Field(min_length=1, max_length=1000)
    is_blocking: bool = Field(alias="isBlocking")
    related_district: Optional[str] = Field(default=None, alias="relatedDistrict")
    related_item_reference: Optional[str] = Field(
        default=None, alias="relatedItemReference"
    )


class ItineraryContextItem(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    reference: str = Field(min_length=1, max_length=200)
    title: Optional[str] = Field(default=None, max_length=500)
    district: Optional[str] = Field(default=None, max_length=100)


class TravelValidationInput(BaseModel):
    """The authoritative structured validation state consumed by the agent."""

    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    validation_result_id: UUID = Field(alias="validationResultId")
    trip_reference: Optional[str] = Field(
        default=None, alias="tripReference", max_length=200
    )
    overall_status: ValidationOverallStatus = Field(alias="overallStatus")
    risk_level: RiskLevel = Field(alias="riskLevel")
    is_feasible: bool = Field(alias="isFeasible")
    total_issue_count: int = Field(alias="totalIssueCount", ge=0)
    blocking_issue_count: int = Field(alias="blockingIssueCount", ge=0)
    issues: List[ValidationIssueInput] = Field(default_factory=list)
    itinerary_items: List[ItineraryContextItem] = Field(
        default_factory=list, alias="itineraryItems"
    )


class RecommendationAction(str, Enum):
    PROCEED = "Proceed"
    PROCEED_WITH_CAUTION = "ProceedWithCaution"
    RESCHEDULE = "Reschedule"
    REROUTE = "Reroute"
    REVIEW_BUDGET = "ReviewBudget"
    RESOLVE_SCHEDULE_CONFLICT = "ResolveScheduleConflict"
    MANUAL_REVIEW = "ManualReview"


class Recommendation(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    action: RecommendationAction
    explanation: str = Field(min_length=1, max_length=1000)
    affected_item_references: List[str] = Field(
        default_factory=list, alias="affectedItemReferences"
    )


class AgentExecutionMetadata(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    agent_name: str = Field(alias="agentName")
    agent_version: str = Field(alias="agentVersion")
    validation_result_id: UUID = Field(alias="validationResultId")
    provider: str
    used_fallback: bool = Field(alias="usedFallback")
    execution_status: str = Field(alias="executionStatus")
    fallback_reason: Optional[str] = Field(default=None, alias="fallbackReason")


class TravelRecommendationOutput(BaseModel):
    """Strict, advisory output with no executable booking command."""

    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    summary: str = Field(min_length=1, max_length=2000)
    risk_level: RiskLevel = Field(alias="riskLevel")
    recommended_action: RecommendationAction = Field(alias="recommendedAction")
    recommendations: List[Recommendation] = Field(default_factory=list)
    requires_human_approval: bool = Field(alias="requiresHumanApproval")
    affected_item_references: List[str] = Field(
        default_factory=list, alias="affectedItemReferences"
    )
    validation_result_id: UUID = Field(alias="validationResultId")
    is_feasible: bool = Field(alias="isFeasible")
    execution: AgentExecutionMetadata
