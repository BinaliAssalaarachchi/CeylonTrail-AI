"""Travel Intelligence Agent with an explicit, safe investigation workflow."""

from dataclasses import dataclass
from time import perf_counter
from typing import Optional
from uuid import UUID, uuid4

from .prompts import TRAVEL_INTELLIGENCE_SYSTEM_POLICY
from .providers import RecommendationProvider
from .schemas import (
    AgentExecutionMetadata,
    AgentObjective,
    ExecutedStep,
    InvestigationPlan,
    InvestigationStep,
    InvestigationStepStatus,
    Recommendation,
    RecommendationAction,
    ProviderRecommendation,
    RiskLevel,
    TravelRecommendationOutput,
    TravelValidationInput,
    ToolExecutionResult,
    ValidationIssueType,
)
from .tools import (
    UnknownToolError,
    build_affected_item_list,
    choose_recommendation_action,
    execute_tool,
    identify_blocking_issues,
    identify_highest_risk,
    summarize_validation_issues,
)


OBJECTIVE = AgentObjective(
    name="travel_intelligence_assessment",
    description=(
        "Assess the validated travel itinerary, investigate identified travel "
        "risks, and produce a safe advisory recommendation for human review."
    ),
)


@dataclass
class PlanExecution:
    workflow_id: UUID
    objective: AgentObjective
    plan: InvestigationPlan
    executed_steps: list[ExecutedStep]
    tool_results: dict[str, object]


class TravelIntelligenceAgent:
    """Produces advisory recommendations from trusted validation data.

    The plan and explicit tool registry are deliberately deterministic today so
    a future provider can request only known, read-only capabilities.
    """

    name = "TravelIntelligenceValidationAgent"
    version = "1.1"

    def __init__(self, provider: Optional[RecommendationProvider] = None):
        self.provider = provider

    def analyze(self, validation: TravelValidationInput) -> TravelRecommendationOutput:
        """Run the plan, then use an optional provider behind safety enforcement."""

        plan_execution = self._execute_plan(validation)
        if self.provider is not None:
            provider_started = perf_counter()
            try:
                provider_output = self.provider.recommend(
                    validation, TRAVEL_INTELLIGENCE_SYSTEM_POLICY
                )
                return self._enforce_authority(
                    validation,
                    provider_output,
                    provider_name=getattr(self.provider, "name", "external"),
                    model_name=getattr(self.provider, "model", None),
                    provider_latency_ms=max(
                        0, int((perf_counter() - provider_started) * 1000)
                    ),
                    plan_execution=plan_execution,
                )
            except Exception as error:  # Provider failures must not break recovery.
                return self._fallback(
                    validation,
                    reason=f"{type(error).__name__}: provider unavailable",
                    provider_attempted=True,
                    provider_latency_ms=max(
                        0, int((perf_counter() - provider_started) * 1000)
                    ),
                    provider_attempt_count=1,
                    model_name=getattr(self.provider, "model", None),
                    attempted_provider_name=getattr(self.provider, "name", "external"),
                    plan_execution=plan_execution,
                )
        return self._fallback(
            validation,
            reason="No recommendation provider configured",
            plan_execution=plan_execution,
        )

    @staticmethod
    def build_investigation_plan() -> InvestigationPlan:
        """Create the fixed plan used by every analysis."""

        return InvestigationPlan(
            steps=[
                InvestigationStep(
                    stepId="review_validation",
                    name="Review authoritative validation state",
                    purpose="Read deterministic feasibility, risk, and issue counts.",
                    toolName="summarize_validation",
                ),
                InvestigationStep(
                    stepId="identify_blocking_issues",
                    name="Identify blocking issues",
                    purpose="Separate blocking issues from advisory warnings.",
                    toolName="list_blocking_issues",
                ),
                InvestigationStep(
                    stepId="identify_affected_items",
                    name="Identify affected itinerary references",
                    purpose="Build a stable list of affected itinerary references.",
                    toolName="identify_affected_items",
                ),
                InvestigationStep(
                    stepId="assess_travel_risk",
                    name="Assess overall travel risk",
                    purpose="Preserve the authoritative risk and feasibility state.",
                    toolName="assess_travel_risk",
                ),
                InvestigationStep(
                    stepId="build_candidates",
                    name="Build safe recommendation candidates",
                    purpose="Create finite advisory actions without executing them.",
                    toolName="build_recommendation_candidates",
                ),
                InvestigationStep(
                    stepId="evaluate_safe_windows",
                    name="Evaluate safe time windows where possible",
                    purpose="Suggest only conditionally safe windows from supplied context.",
                    toolName="find_safe_time_windows",
                ),
                InvestigationStep(
                    stepId="finalize_recommendation",
                    name="Finalize recommendation under safety constraints",
                    purpose="Construct an advisory result for later human review.",
                ),
            ]
        )

    def _execute_plan(self, validation: TravelValidationInput) -> PlanExecution:
        workflow_id = uuid4()
        plan = self.build_investigation_plan()
        executed_steps: list[ExecutedStep] = []
        tool_results: dict[str, object] = {}

        for step in plan.steps:
            step.status = InvestigationStepStatus.RUNNING
            started = perf_counter()
            try:
                if step.tool_name is not None:
                    result = execute_tool(step.tool_name, validation)
                    tool_results[step.tool_name] = result
                    result_summary = result.summary
                else:
                    result_summary = "Recommendation construction reserved for the safety boundary."
                step.status = InvestigationStepStatus.COMPLETED
            except Exception as error:
                step.status = InvestigationStepStatus.FAILED
                result_summary = f"Step failed safely: {type(error).__name__}."
                executed_steps.append(
                    ExecutedStep(
                        stepId=step.step_id,
                        toolName=step.tool_name,
                        status=step.status,
                        durationMs=max(0, int((perf_counter() - started) * 1000)),
                        resultSummary=result_summary,
                    )
                )
                raise UnknownToolError(result_summary) from error

            executed_steps.append(
                ExecutedStep(
                    stepId=step.step_id,
                    toolName=step.tool_name,
                    status=step.status,
                    durationMs=max(0, int((perf_counter() - started) * 1000)),
                    resultSummary=result_summary,
                )
            )

        return PlanExecution(workflow_id, OBJECTIVE, plan, executed_steps, tool_results)

    def _fallback(
        self,
        validation: TravelValidationInput,
        reason: str,
        plan_execution: Optional[PlanExecution] = None,
        provider_attempted: bool = False,
        provider_succeeded: bool = False,
        provider_latency_ms: Optional[int] = None,
        provider_attempt_count: int = 0,
        model_name: Optional[str] = None,
        provider_name: str = "deterministic-fallback",
        attempted_provider_name: Optional[str] = None,
    ) -> TravelRecommendationOutput:
        plan_execution = plan_execution or self._execute_plan(validation)
        candidate_result = plan_execution.tool_results.get("build_recommendation_candidates")
        # Candidate actions are advisory alternatives. The deterministic chooser
        # remains authoritative for the primary recommendation.
        action = choose_recommendation_action(validation)
        affected_result = plan_execution.tool_results.get("identify_affected_items")
        affected = (
            affected_result.affected_item_references
            if isinstance(affected_result, ToolExecutionResult)
            else build_affected_item_list(validation)
        )
        affected_items = (
            affected_result.affected_items
            if isinstance(affected_result, ToolExecutionResult)
            else []
        )
        alternatives = (
            candidate_result.alternatives
            if isinstance(candidate_result, ToolExecutionResult)
            else []
        )
        safe_windows_result = plan_execution.tool_results.get("find_safe_time_windows")
        safe_windows = (
            safe_windows_result.safe_windows
            if isinstance(safe_windows_result, ToolExecutionResult)
            else []
        )
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
            affectedItems=affected_items,
            alternatives=alternatives,
            safeWindows=safe_windows,
            validationResultId=validation.validation_result_id,
            isFeasible=validation.is_feasible,
            execution=self._execution_metadata(
                validation,
                plan_execution,
                provider=provider_name,
                used_fallback=True,
                status="Fallback",
                reason=reason,
                result_summary="Deterministic advisory fallback completed safely.",
                model_name=model_name,
                provider_attempted=provider_attempted,
                provider_succeeded=provider_succeeded,
                attempted_provider_name=attempted_provider_name,
                provider_latency_ms=provider_latency_ms,
                provider_attempt_count=provider_attempt_count,
            ),
        )

    def _enforce_authority(
        self,
        validation: TravelValidationInput,
        provider_output: ProviderRecommendation | TravelRecommendationOutput,
        provider_name: str,
        model_name: Optional[str],
        provider_latency_ms: Optional[int],
        plan_execution: PlanExecution,
    ) -> TravelRecommendationOutput:
        """Prevent provider output from changing authoritative validation state."""

        fallback = self._fallback(
            validation,
            reason="Provider output normalized",
            plan_execution=plan_execution,
        )
        if isinstance(provider_output, TravelRecommendationOutput):
            provider_summary = provider_output.summary
            provider_rationale = None
        elif isinstance(provider_output, ProviderRecommendation):
            provider_summary = provider_output.summary
            provider_rationale = provider_output.rationale
        else:
            raise TypeError("Provider returned an unsupported response type.")

        provider_recommendations = fallback.recommendations
        if provider_rationale:
            provider_recommendations = [
                *provider_recommendations,
                Recommendation(
                    action=fallback.recommended_action,
                    explanation=provider_rationale,
                    affectedItemReferences=fallback.affected_item_references,
                ),
            ]

        execution = self._execution_metadata(
            validation,
            plan_execution,
            provider=provider_name,
            used_fallback=False,
            status="Completed",
            reason=None,
            result_summary="Provider advisory normalized against deterministic validation.",
            model_name=model_name,
            provider_attempted=True,
            provider_succeeded=True,
            attempted_provider_name=provider_name,
            provider_latency_ms=provider_latency_ms,
            provider_attempt_count=1,
        )
        if isinstance(provider_output, TravelRecommendationOutput):
            return provider_output.model_copy(
                update={
                    "summary": provider_summary,
                    "risk_level": validation.risk_level,
                    "recommended_action": fallback.recommended_action,
                    "recommendations": provider_recommendations,
                    "requires_human_approval": fallback.requires_human_approval,
                    "affected_item_references": fallback.affected_item_references,
                    "affected_items": fallback.affected_items,
                    "alternatives": fallback.alternatives,
                    "safe_windows": fallback.safe_windows,
                    "validation_result_id": validation.validation_result_id,
                    "is_feasible": validation.is_feasible,
                    "execution": execution,
                }
            )

        return fallback.model_copy(
            update={
                "summary": provider_summary,
                "recommendations": provider_recommendations,
                "execution": execution,
            }
        )

    @staticmethod
    def _execution_metadata(
        validation: TravelValidationInput,
        plan_execution: PlanExecution,
        provider: str,
        used_fallback: bool,
        status: str,
        reason: Optional[str],
        result_summary: str,
        model_name: Optional[str] = None,
        provider_attempted: bool = False,
        provider_succeeded: bool = False,
        attempted_provider_name: Optional[str] = None,
        provider_latency_ms: Optional[int] = None,
        provider_attempt_count: int = 0,
    ) -> AgentExecutionMetadata:
        executed_steps = plan_execution.executed_steps
        last_step = executed_steps[-1] if executed_steps else None
        return AgentExecutionMetadata(
            agentName=TravelIntelligenceAgent.name,
            agentVersion=TravelIntelligenceAgent.version,
            validationResultId=validation.validation_result_id,
            provider=provider,
            usedFallback=used_fallback,
            executionStatus=status,
            fallbackReason=reason,
            workflowId=plan_execution.workflow_id,
            objective=plan_execution.objective,
            investigationPlan=plan_execution.plan,
            executedSteps=executed_steps,
            executedStepId=last_step.step_id if last_step else None,
            executedToolName=last_step.tool_name if last_step else None,
            durationMs=sum(step.duration_ms for step in executed_steps),
            resultSummary=result_summary,
            modelName=model_name,
            providerAttempted=provider_attempted,
            providerSucceeded=provider_succeeded,
            providerName=attempted_provider_name,
            providerLatencyMs=provider_latency_ms,
            providerAttemptCount=provider_attempt_count,
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
