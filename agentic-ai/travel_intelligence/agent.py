"""Travel Intelligence Agent with an explicit, safe investigation workflow."""

from dataclasses import dataclass, field
import logging
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
    ToolRequest,
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
    TOOL_REGISTRY,
)


OBJECTIVE = AgentObjective(
    name="travel_intelligence_assessment",
    description=(
        "Assess the validated travel itinerary, investigate identified travel "
        "risks, and produce a safe advisory recommendation for human review."
    ),
)


PLAN_STEP_TOOL_PERMISSIONS = {
    "review_validation": frozenset({"summarize_validation"}),
    "identify_blocking_issues": frozenset({"list_blocking_issues"}),
    "identify_affected_items": frozenset({"identify_affected_items"}),
    "assess_travel_risk": frozenset({"assess_travel_risk"}),
    "build_candidates": frozenset({"build_recommendation_candidates"}),
    "evaluate_safe_windows": frozenset({"find_safe_time_windows"}),
}

MAX_TOOL_SELECTION_ATTEMPTS = 1
MAX_EXECUTED_TOOLS = 6

logger = logging.getLogger(__name__)


class DeterministicExecutionError(RuntimeError):
    """Raised when the deterministic investigation cannot complete safely."""


@dataclass
class PlanExecution:
    workflow_id: UUID
    objective: AgentObjective
    plan: InvestigationPlan
    executed_steps: list[ExecutedStep]
    tool_results: dict[str, object]
    tool_selection_provider_attempted: bool = False
    selected_tool_names: list[str] = field(default_factory=list)
    rejected_tool_names: list[str] = field(default_factory=list)
    tool_selection_fallback_used: bool = False
    tool_selection_fallback_reason: Optional[str] = None
    selection_attempt_count: int = 0


@dataclass
class ToolSelectionState:
    provider_attempted: bool = False
    selected_tool_names: list[str] = field(default_factory=list)
    rejected_tool_names: list[str] = field(default_factory=list)
    fallback_used: bool = False
    fallback_reason: Optional[str] = None
    attempt_count: int = 0


class TravelIntelligenceAgent:
    """Produces advisory recommendations from trusted validation data.

    The plan and explicit tool registry remain deterministic. A provider may
    propose only a known, read-only capability for the current step; this class
    remains the sole executor.
    """

    name = "TravelIntelligenceValidationAgent"
    version = "1.1"

    def __init__(self, provider: Optional[RecommendationProvider] = None):
        self.provider = provider

    def analyze(self, validation: TravelValidationInput) -> TravelRecommendationOutput:
        """Run the plan, then use an optional provider behind safety enforcement."""

        try:
            plan_execution = self._execute_plan(validation, self.provider)
        except Exception as error:
            logger.warning(
                "Travel Intelligence deterministic execution failed "
                "stage=investigation_plan exception_type=%s",
                type(error).__name__,
            )
            raise DeterministicExecutionError(
                "Deterministic Travel Intelligence execution is unavailable."
            ) from error
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

    def _execute_plan(
        self,
        validation: TravelValidationInput,
        provider: Optional[RecommendationProvider] = None,
    ) -> PlanExecution:
        workflow_id = uuid4()
        plan = self.build_investigation_plan()
        executed_steps: list[ExecutedStep] = []
        tool_results: dict[str, object] = {}
        executed_tool_names: list[str] = []
        selection = ToolSelectionState()

        for step in plan.steps:
            step.status = InvestigationStepStatus.RUNNING
            started = perf_counter()
            try:
                if step.tool_name is not None:
                    if len(executed_tool_names) >= MAX_EXECUTED_TOOLS:
                        raise UnknownToolError("Maximum deterministic tool count reached.")
                    selected_tool = self._select_tool_for_step(
                        validation,
                        step,
                        provider,
                        executed_tool_names,
                        selection,
                    )
                    result = execute_tool(selected_tool, validation)
                    executed_tool_names.append(selected_tool)
                    tool_results[selected_tool] = result
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

        return PlanExecution(
            workflow_id,
            OBJECTIVE,
            plan,
            executed_steps,
            tool_results,
            selection.provider_attempted,
            selection.selected_tool_names,
            selection.rejected_tool_names,
            selection.fallback_used,
            selection.fallback_reason,
            selection.attempt_count,
        )

    @staticmethod
    def _select_tool_for_step(
        validation: TravelValidationInput,
        step: InvestigationStep,
        provider: Optional[RecommendationProvider],
        executed_tool_names: list[str],
        selection: ToolSelectionState,
    ) -> str:
        """Ask the provider for one tool, then enforce the deterministic boundary."""

        deterministic_tool = step.tool_name
        if deterministic_tool is None or provider is None:
            return deterministic_tool or ""

        selector = getattr(provider, "select_tool", None)
        if not callable(selector):
            return deterministic_tool

        # A step may only have one provider-controlled selection. If the same
        # step is evaluated again after its tool was selected or executed,
        # record the duplicate before the provider-attempt budget is checked.
        if (
            deterministic_tool in selection.selected_tool_names
            or deterministic_tool in executed_tool_names
        ):
            selection.fallback_used = True
            selection.fallback_reason = "provider requested a duplicate tool"
            if deterministic_tool not in selection.rejected_tool_names:
                selection.rejected_tool_names.append(deterministic_tool)
            return deterministic_tool

        if selection.attempt_count >= MAX_TOOL_SELECTION_ATTEMPTS:
            selection.fallback_used = True
            selection.fallback_reason = "tool-selection attempt limit reached"
            return deterministic_tool

        selection.provider_attempted = True
        selection.attempt_count += 1
        allowed = PLAN_STEP_TOOL_PERMISSIONS.get(step.step_id, frozenset())
        try:
            request = selector(
                validation,
                OBJECTIVE.name,
                step.step_id,
                sorted(allowed),
                list(executed_tool_names),
            )
        except Exception:
            selection.fallback_used = True
            selection.fallback_reason = "tool-selection provider failed"
            return deterministic_tool

        if not isinstance(request, ToolRequest):
            selection.fallback_used = True
            selection.fallback_reason = "tool-selection response was invalid"
            selection.rejected_tool_names.append("<invalid>")
            return deterministic_tool

        requested_tool = request.tool_name
        if requested_tool not in TOOL_REGISTRY:
            selection.fallback_used = True
            selection.fallback_reason = "provider requested an unknown tool"
            selection.rejected_tool_names.append(requested_tool)
            return deterministic_tool
        if requested_tool not in allowed:
            selection.fallback_used = True
            selection.fallback_reason = "provider requested a wrong-step tool"
            selection.rejected_tool_names.append(requested_tool)
            return deterministic_tool
        if requested_tool in executed_tool_names:
            selection.fallback_used = True
            selection.fallback_reason = "provider requested a duplicate tool"
            selection.rejected_tool_names.append(requested_tool)
            return deterministic_tool

        selection.selected_tool_names.append(requested_tool)
        return requested_tool

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
        plan_execution = plan_execution or self._execute_plan(validation, self.provider)
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
            toolSelectionProviderAttempted=plan_execution.tool_selection_provider_attempted,
            selectedToolNames=plan_execution.selected_tool_names,
            rejectedToolNames=plan_execution.rejected_tool_names,
            toolSelectionFallbackUsed=plan_execution.tool_selection_fallback_used,
            toolSelectionFallbackReason=plan_execution.tool_selection_fallback_reason,
            selectionAttemptCount=plan_execution.selection_attempt_count,
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
