"""Travel Intelligence & Validation Agent with deterministic fallback."""

from typing import Optional

from .prompts import TRAVEL_INTELLIGENCE_SYSTEM_POLICY
from .providers import RecommendationProvider
from .schemas import (
    AgentExecutionMetadata,
    Recommendation,
    RecommendationAction,
    RiskLevel,
    TravelRecommendationOutput,
    TravelValidationInput,
    ValidationIssueType,
)
from .tools import (
    build_affected_item_list,
    choose_recommendation_action,
    identify_blocking_issues,
    identify_highest_risk,
    summarize_validation_issues,
)


class TravelIntelligenceAgent:
    """Produces explainable advisory recommendations from trusted validation data."""

    name = "TravelIntelligenceValidationAgent"
    version = "1.0"

    def __init__(self, provider: Optional[RecommendationProvider] = None):
        self.provider = provider

    def analyze(self, validation: TravelValidationInput) -> TravelRecommendationOutput:
        """Use an optional provider, enforcing deterministic authority at the boundary."""

        if self.provider is not None:
            try:
                provider_output = self.provider.recommend(
                    validation, TRAVEL_INTELLIGENCE_SYSTEM_POLICY
                )
                return self._enforce_authority(
                    validation, provider_output, provider_name="external"
                )
            except Exception as error:  # Provider failures must not break recovery.
                return self._fallback(
                    validation,
                    reason=f"{type(error).__name__}: provider unavailable",
                )
        return self._fallback(validation, reason="No recommendation provider configured")

    def _fallback(
        self,
        validation: TravelValidationInput,
        reason: str,
    ) -> TravelRecommendationOutput:
        action = choose_recommendation_action(validation)
        affected = build_affected_item_list(validation)
        blocking = identify_blocking_issues(validation)
        summaries = summarize_validation_issues(validation)
        recommendations = [
            self._recommendation_for_issue(issue, affected)
            for issue in validation.issues
        ]

        return TravelRecommendationOutput(
            summary=(
                f"Deterministic validation is {validation.overall_status.value} with "
                f"{summaries['total']} issue(s), including {summaries['blocking']} blocking issue(s)."
            ),
            riskLevel=identify_highest_risk(validation),
            recommendedAction=action,
            recommendations=recommendations,
            requiresHumanApproval=bool(blocking)
            or action
            in {
                RecommendationAction.RESCHEDULE,
                RecommendationAction.REROUTE,
                RecommendationAction.REVIEW_BUDGET,
                RecommendationAction.RESOLVE_SCHEDULE_CONFLICT,
                RecommendationAction.MANUAL_REVIEW,
            },
            affectedItemReferences=affected,
            validationResultId=validation.validation_result_id,
            isFeasible=validation.is_feasible,
            execution=AgentExecutionMetadata(
                agentName=self.name,
                agentVersion=self.version,
                validationResultId=validation.validation_result_id,
                provider="deterministic-fallback",
                usedFallback=True,
                executionStatus="Fallback",
                fallbackReason=reason,
            ),
        )

    def _enforce_authority(
        self,
        validation: TravelValidationInput,
        provider_output: TravelRecommendationOutput,
        provider_name: str,
    ) -> TravelRecommendationOutput:
        """Prevent provider output from changing authoritative validation state."""

        fallback = self._fallback(validation, reason="Provider output normalized")
        action = provider_output.recommended_action
        if not validation.is_feasible and action == RecommendationAction.PROCEED:
            action = fallback.recommended_action

        return provider_output.model_copy(
            update={
                "summary": fallback.summary,
                "risk_level": validation.risk_level,
                "recommended_action": action,
                "requires_human_approval": fallback.requires_human_approval,
                "affected_item_references": fallback.affected_item_references,
                "validation_result_id": validation.validation_result_id,
                "is_feasible": validation.is_feasible,
                "execution": AgentExecutionMetadata(
                    agent_name=self.name,
                    agent_version=self.version,
                    validation_result_id=validation.validation_result_id,
                    provider=provider_name,
                    used_fallback=False,
                    execution_status="Completed",
                ),
            }
        )

    @staticmethod
    def _recommendation_for_issue(issue, affected):
        refs = (
            [reference.strip() for reference in issue.related_item_reference.split(",")]
            if issue.related_item_reference
            else []
        )
        refs = [reference for reference in refs if reference]
        if issue.issue_type == ValidationIssueType.TRAVEL_ALERT:
            action = (
                RecommendationAction.RESCHEDULE
                if issue.severity == RiskLevel.CRITICAL
                else RecommendationAction.PROCEED_WITH_CAUTION
            )
            explanation = (
                f"{issue.severity.value} travel alert requires review before "
                f"the affected item is undertaken."
            )
        elif issue.issue_type == ValidationIssueType.BUDGET_EXCEEDED:
            action = RecommendationAction.REVIEW_BUDGET
            explanation = "Review the budget or adjust itinerary costs."
        elif issue.issue_type == ValidationIssueType.SCHEDULE_CONFLICT:
            action = RecommendationAction.RESOLVE_SCHEDULE_CONFLICT
            explanation = "Adjust the conflicting activity times."
        elif issue.issue_type == ValidationIssueType.INVALID_TIME_RANGE:
            action = RecommendationAction.RESOLVE_SCHEDULE_CONFLICT
            explanation = "Correct the activity start and end times."
        else:
            action = RecommendationAction.MANUAL_REVIEW
            explanation = "Review this validation issue before proceeding."
        return Recommendation(
            action=action,
            explanation=explanation,
            affectedItemReferences=refs or affected,
        )
