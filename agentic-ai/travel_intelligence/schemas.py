"""Strict schemas exchanged with the deterministic ASP.NET validation boundary."""

from enum import Enum
from datetime import datetime
from decimal import Decimal
from typing import List, Optional
from uuid import UUID, uuid4

from pydantic import BaseModel, ConfigDict, Field

from agent_trace import AgentExecutionTrace


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

    reference: str = Field(alias="itemReference", min_length=1, max_length=200)
    title: Optional[str] = Field(default=None, max_length=500)
    district: Optional[str] = Field(default=None, max_length=100)
    start_date_time: Optional[datetime] = Field(default=None, alias="startDateTime")
    end_date_time: Optional[datetime] = Field(default=None, alias="endDateTime")
    estimated_cost: Optional[Decimal] = Field(default=None, alias="estimatedCost", ge=0)


class TravelAlertWindow(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    district: str = Field(min_length=1, max_length=100)
    start_date_time: datetime = Field(alias="startDateTime")
    end_date_time: datetime = Field(alias="endDateTime")


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
    blocking_travel_alert_windows: List[TravelAlertWindow] = Field(
        default_factory=list, alias="blockingTravelAlertWindows", max_length=100
    )


class AgentObjective(BaseModel):
    """Trusted system objective; it is never taken from issue text or a model."""

    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    name: str = Field(min_length=1, max_length=100)
    description: str = Field(min_length=1, max_length=500)
    source: str = Field(default="system", min_length=1, max_length=30)


class ToolRequest(BaseModel):
    """A bounded request for one existing investigation tool."""

    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    tool_name: str = Field(alias="toolName", min_length=1, max_length=80)
    rationale: str = Field(min_length=1, max_length=300)


class InvestigationStepStatus(str, Enum):
    PENDING = "Pending"
    RUNNING = "Running"
    COMPLETED = "Completed"
    FAILED = "Failed"
    SKIPPED = "Skipped"


class InvestigationStep(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    step_id: str = Field(alias="stepId", min_length=1, max_length=80)
    name: str = Field(min_length=1, max_length=150)
    purpose: str = Field(min_length=1, max_length=500)
    tool_name: Optional[str] = Field(default=None, alias="toolName", max_length=80)
    status: InvestigationStepStatus = InvestigationStepStatus.PENDING


class InvestigationPlan(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    steps: List[InvestigationStep] = Field(min_length=1, max_length=20)


def default_investigation_plan() -> InvestigationPlan:
    return InvestigationPlan(steps=[
        InvestigationStep(
            stepId="review_validation",
            name="Review authoritative validation state",
            purpose="Read the deterministic feasibility, risk, and issue state.",
            toolName="summarize_validation",
        ),
    ])


class RecommendationAction(str, Enum):
    PROCEED = "Proceed"
    PROCEED_WITH_CAUTION = "ProceedWithCaution"
    RESCHEDULE = "Reschedule"
    REROUTE = "Reroute"
    REVIEW_BUDGET = "ReviewBudget"
    RESOLVE_SCHEDULE_CONFLICT = "ResolveScheduleConflict"
    MANUAL_REVIEW = "ManualReview"


class ProviderRecommendation(BaseModel):
    """The only recommendation data an external provider may author."""

    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    proposed_action: RecommendationAction = Field(alias="proposedAction")
    summary: str = Field(min_length=1, max_length=1000)
    rationale: str = Field(min_length=1, max_length=1500)


class SafetyStatus(str, Enum):
    CONDITIONALLY_SAFE = "ConditionallySafe"
    MANUAL_REVIEW_REQUIRED = "ManualReviewRequired"
    NOT_AVAILABLE = "NotAvailable"


class AffectedItemAnalysis(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    item_reference: str = Field(alias="itemReference", min_length=1, max_length=200)
    title: Optional[str] = Field(default=None, max_length=500)
    district: Optional[str] = Field(default=None, max_length=100)
    start_date_time: Optional[datetime] = Field(default=None, alias="startDateTime")
    end_date_time: Optional[datetime] = Field(default=None, alias="endDateTime")
    estimated_cost: Optional[Decimal] = Field(default=None, alias="estimatedCost", ge=0)
    issue_types: List[ValidationIssueType] = Field(
        default_factory=list, alias="issueTypes", max_length=20
    )
    highest_issue_severity: Optional[RiskLevel] = Field(
        default=None, alias="highestIssueSeverity"
    )
    is_blocking: bool = Field(alias="isBlocking")
    details_available: bool = Field(alias="detailsAvailable")


class AlternativeRecommendation(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    alternative_id: str = Field(alias="alternativeId", min_length=1, max_length=80)
    action: RecommendationAction
    affected_item_references: List[str] = Field(
        default_factory=list, alias="affectedItemReferences", max_length=100
    )
    rationale: str = Field(min_length=1, max_length=1000)
    safety_status: SafetyStatus = Field(alias="safetyStatus")
    requires_human_approval: bool = Field(alias="requiresHumanApproval")
    constraints: List[str] = Field(default_factory=list, max_length=10)


class SafeWindowSuggestion(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    item_reference: str = Field(alias="itemReference", min_length=1, max_length=200)
    proposed_start: datetime = Field(alias="proposedStart")
    proposed_end: datetime = Field(alias="proposedEnd")
    reason: str = Field(min_length=1, max_length=1000)
    safety_status: SafetyStatus = Field(alias="safetyStatus")
    constraints: List[str] = Field(default_factory=list, max_length=10)


class ToolExecutionResult(BaseModel):
    """Safe, bounded result envelope shared by allow-listed tools."""

    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    tool_name: str = Field(alias="toolName", min_length=1, max_length=80)
    summary: str = Field(min_length=1, max_length=500)
    total_issue_count: Optional[int] = Field(default=None, alias="totalIssueCount", ge=0)
    blocking_issue_count: Optional[int] = Field(default=None, alias="blockingIssueCount", ge=0)
    affected_item_references: List[str] = Field(
        default_factory=list, alias="affectedItemReferences", max_length=100
    )
    risk_level: Optional[RiskLevel] = Field(default=None, alias="riskLevel")
    is_feasible: Optional[bool] = Field(default=None, alias="isFeasible")
    candidate_actions: List[RecommendationAction] = Field(
        default_factory=list, alias="candidateActions", max_length=20
    )
    affected_items: List[AffectedItemAnalysis] = Field(
        default_factory=list, alias="affectedItems", max_length=100
    )
    alternatives: List[AlternativeRecommendation] = Field(
        default_factory=list, max_length=3
    )
    safe_windows: List[SafeWindowSuggestion] = Field(
        default_factory=list, alias="safeWindows", max_length=20
    )


class ExecutedStep(BaseModel):
    model_config = ConfigDict(extra="forbid", populate_by_name=True)

    step_id: str = Field(alias="stepId", min_length=1, max_length=80)
    tool_name: Optional[str] = Field(default=None, alias="toolName", max_length=80)
    status: InvestigationStepStatus
    duration_ms: int = Field(alias="durationMs", ge=0)
    result_summary: str = Field(alias="resultSummary", min_length=1, max_length=500)


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
    workflow_id: UUID = Field(default_factory=uuid4, alias="workflowId")
    objective: AgentObjective = Field(default_factory=lambda: AgentObjective(
        name="travel_intelligence_assessment",
        description="Assess the validated travel itinerary, investigate identified travel risks, and produce a safe advisory recommendation for human review.",
    ))
    investigation_plan: InvestigationPlan = Field(
        default_factory=default_investigation_plan, alias="investigationPlan"
    )
    executed_steps: List[ExecutedStep] = Field(default_factory=list, alias="executedSteps")
    executed_step_id: Optional[str] = Field(default=None, alias="executedStepId")
    executed_tool_name: Optional[str] = Field(default=None, alias="executedToolName")
    duration_ms: int = Field(default=0, alias="durationMs", ge=0)
    result_summary: str = Field(default="", alias="resultSummary", max_length=500)
    model_name: Optional[str] = Field(default=None, alias="modelName", max_length=120)
    provider_attempted: bool = Field(default=False, alias="providerAttempted")
    provider_succeeded: bool = Field(default=False, alias="providerSucceeded")
    provider_name: Optional[str] = Field(default=None, alias="providerName", max_length=120)
    provider_latency_ms: Optional[int] = Field(
        default=None, alias="providerLatencyMs", ge=0
    )
    provider_attempt_count: int = Field(default=0, alias="providerAttemptCount", ge=0)
    tool_selection_provider_attempted: bool = Field(
        default=False, alias="toolSelectionProviderAttempted"
    )
    selected_tool_names: List[str] = Field(
        default_factory=list, alias="selectedToolNames", max_length=20
    )
    rejected_tool_names: List[str] = Field(
        default_factory=list, alias="rejectedToolNames", max_length=20
    )
    tool_selection_fallback_used: bool = Field(
        default=False, alias="toolSelectionFallbackUsed"
    )
    tool_selection_fallback_reason: Optional[str] = Field(
        default=None, alias="toolSelectionFallbackReason", max_length=300
    )
    selection_attempt_count: int = Field(
        default=0, alias="selectionAttemptCount", ge=0, le=20
    )


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
    affected_items: List[AffectedItemAnalysis] = Field(
        default_factory=list, alias="affectedItems", max_length=100
    )
    alternatives: List[AlternativeRecommendation] = Field(
        default_factory=list, max_length=3
    )
    safe_windows: List[SafeWindowSuggestion] = Field(
        default_factory=list, alias="safeWindows", max_length=20
    )
    validation_result_id: UUID = Field(alias="validationResultId")
    is_feasible: bool = Field(alias="isFeasible")
    execution: AgentExecutionMetadata
    trace: Optional[AgentExecutionTrace] = None
