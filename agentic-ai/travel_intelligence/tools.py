"""Controlled, deterministic tools used by the Travel Intelligence Agent."""

from collections import Counter
from typing import Dict, List

from .schemas import (
    RecommendationAction,
    RiskLevel,
    TravelValidationInput,
    ValidationIssueInput,
    ValidationIssueType,
    ToolExecutionResult,
)


class UnknownToolError(ValueError):
    """Raised when a requested capability is not in the fixed tool registry."""


def summarize_validation_issues(
    validation: TravelValidationInput,
) -> Dict[str, object]:
    """Return structured issue counts without interpreting descriptive text."""

    return {
        "total": len(validation.issues),
        "blocking": sum(issue.is_blocking for issue in validation.issues),
        "by_type": dict(Counter(issue.issue_type.value for issue in validation.issues)),
        "by_severity": dict(Counter(issue.severity.value for issue in validation.issues)),
    }


def identify_blocking_issues(
    validation: TravelValidationInput,
) -> List[ValidationIssueInput]:
    """Return only issues marked blocking by deterministic validation."""

    return [issue for issue in validation.issues if issue.is_blocking]


def identify_highest_risk(validation: TravelValidationInput) -> RiskLevel:
    """Preserve the authoritative risk level supplied by ASP.NET."""

    return validation.risk_level


def build_affected_item_list(validation: TravelValidationInput) -> List[str]:
    """Build a stable, de-duplicated list of referenced affected items."""

    references: List[str] = []
    for issue in validation.issues:
        reference = issue.related_item_reference
        if not reference:
            continue
        for item_reference in reference.split(","):
            cleaned = item_reference.strip()
            if cleaned and cleaned not in references:
                references.append(cleaned)
    return references


def choose_recommendation_action(
    validation: TravelValidationInput,
) -> RecommendationAction:
    """Map trusted issue types/severities to a finite action vocabulary."""

    issues = validation.issues
    if validation.is_feasible and not issues:
        return RecommendationAction.PROCEED

    critical_alert = any(
        issue.issue_type == ValidationIssueType.TRAVEL_ALERT
        and issue.severity == RiskLevel.CRITICAL
        and issue.is_blocking
        for issue in issues
    )
    if critical_alert:
        return RecommendationAction.RESCHEDULE

    if any(
        issue.issue_type == ValidationIssueType.SCHEDULE_CONFLICT
        and issue.is_blocking
        for issue in issues
    ):
        return RecommendationAction.RESOLVE_SCHEDULE_CONFLICT

    if any(
        issue.issue_type == ValidationIssueType.INVALID_TIME_RANGE
        and issue.is_blocking
        for issue in issues
    ):
        return RecommendationAction.RESOLVE_SCHEDULE_CONFLICT

    if any(
        issue.issue_type == ValidationIssueType.BUDGET_EXCEEDED for issue in issues
    ):
        return RecommendationAction.REVIEW_BUDGET

    if validation.is_feasible and any(
        issue.issue_type == ValidationIssueType.TRAVEL_ALERT for issue in issues
    ):
        return RecommendationAction.PROCEED_WITH_CAUTION

    return RecommendationAction.MANUAL_REVIEW


def build_recommendation_candidates(
    validation: TravelValidationInput,
) -> ToolExecutionResult:
    """Return finite advisory actions; this tool never executes an action."""

    candidates = [choose_recommendation_action(validation)]
    for issue in validation.issues:
        if issue.issue_type == ValidationIssueType.TRAVEL_ALERT:
            candidate = (
                RecommendationAction.RESCHEDULE
                if issue.severity == RiskLevel.CRITICAL
                else RecommendationAction.PROCEED_WITH_CAUTION
            )
        elif issue.issue_type == ValidationIssueType.BUDGET_EXCEEDED:
            candidate = RecommendationAction.REVIEW_BUDGET
        elif issue.issue_type in {
            ValidationIssueType.SCHEDULE_CONFLICT,
            ValidationIssueType.INVALID_TIME_RANGE,
        }:
            candidate = RecommendationAction.RESOLVE_SCHEDULE_CONFLICT
        else:
            candidate = RecommendationAction.MANUAL_REVIEW
        if candidate not in candidates:
            candidates.append(candidate)

    return ToolExecutionResult(
        toolName="build_recommendation_candidates",
        summary=f"Built {len(candidates)} bounded advisory action candidate(s).",
        candidateActions=candidates,
    )


def summarize_validation_tool(
    validation: TravelValidationInput,
) -> ToolExecutionResult:
    summary = summarize_validation_issues(validation)
    return ToolExecutionResult(
        toolName="summarize_validation",
        summary="Summarized authoritative validation counts.",
        totalIssueCount=summary["total"],
        blockingIssueCount=summary["blocking"],
        riskLevel=validation.risk_level,
        isFeasible=validation.is_feasible,
    )


def list_blocking_issues_tool(
    validation: TravelValidationInput,
) -> ToolExecutionResult:
    blocking = identify_blocking_issues(validation)
    references = [
        reference.strip()
        for issue in blocking
        for reference in (issue.related_item_reference or "").split(",")
        if reference.strip()
    ]
    return ToolExecutionResult(
        toolName="list_blocking_issues",
        summary=f"Found {len(blocking)} authoritative blocking issue(s).",
        blockingIssueCount=len(blocking),
        affectedItemReferences=list(dict.fromkeys(references)),
    )


def identify_affected_items_tool(
    validation: TravelValidationInput,
) -> ToolExecutionResult:
    references = build_affected_item_list(validation)
    return ToolExecutionResult(
        toolName="identify_affected_items",
        summary=f"Identified {len(references)} affected itinerary reference(s).",
        affectedItemReferences=references,
    )


def assess_travel_risk_tool(
    validation: TravelValidationInput,
) -> ToolExecutionResult:
    return ToolExecutionResult(
        toolName="assess_travel_risk",
        summary=f"Preserved authoritative {validation.risk_level.value} risk and feasibility state.",
        riskLevel=validation.risk_level,
        isFeasible=validation.is_feasible,
        blockingIssueCount=validation.blocking_issue_count,
    )


# This registry is intentionally explicit. Model/provider strings are looked up
# here and can only reach these read-only deterministic functions.
TOOL_REGISTRY = {
    "summarize_validation": summarize_validation_tool,
    "list_blocking_issues": list_blocking_issues_tool,
    "identify_affected_items": identify_affected_items_tool,
    "assess_travel_risk": assess_travel_risk_tool,
    "build_recommendation_candidates": build_recommendation_candidates,
}


def execute_tool(
    tool_name: str,
    validation: TravelValidationInput,
) -> ToolExecutionResult:
    """Execute one registered read-only tool and reject all other names."""

    if not isinstance(validation, TravelValidationInput):
        raise TypeError("Tool context must be a validated TravelValidationInput.")
    tool = TOOL_REGISTRY.get(tool_name)
    if tool is None:
        raise UnknownToolError(f"Unknown travel-intelligence tool: {tool_name}")
    return tool(validation)
