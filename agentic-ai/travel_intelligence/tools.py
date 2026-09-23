"""Controlled, deterministic tools used by the Travel Intelligence Agent."""

from collections import Counter
from datetime import timedelta
from typing import Dict, List

from .schemas import (
    RecommendationAction,
    RiskLevel,
    TravelValidationInput,
    ValidationIssueInput,
    ValidationIssueType,
    AffectedItemAnalysis,
    AlternativeRecommendation,
    SafetyStatus,
    SafeWindowSuggestion,
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
    context_by_reference = {
        item.reference: item for item in validation.itinerary_items
    }
    grouped: dict[str, list[ValidationIssueInput]] = {}
    for issue in validation.issues:
        for reference in (issue.related_item_reference or "").split(","):
            cleaned = reference.strip()
            if cleaned:
                grouped.setdefault(cleaned, []).append(issue)

    affected_items = []
    for reference, issues in grouped.items():
        context = context_by_reference.get(reference)
        severities = [issue.severity for issue in issues]
        severity_order = {
            RiskLevel.LOW: 0,
            RiskLevel.MEDIUM: 1,
            RiskLevel.HIGH: 2,
            RiskLevel.CRITICAL: 3,
        }
        highest = max(severities, key=lambda value: severity_order[value])
        affected_items.append(
            AffectedItemAnalysis(
                itemReference=reference,
                title=context.title if context else None,
                district=context.district if context else None,
                startDateTime=context.start_date_time if context else None,
                endDateTime=context.end_date_time if context else None,
                estimatedCost=context.estimated_cost if context else None,
                issueTypes=list(dict.fromkeys(issue.issue_type for issue in issues)),
                highestIssueSeverity=highest,
                isBlocking=any(issue.is_blocking for issue in issues),
                detailsAvailable=context is not None,
            )
        )

    references = [item.item_reference for item in affected_items]
    return ToolExecutionResult(
        toolName="identify_affected_items",
        summary=f"Analyzed {len(references)} affected itinerary reference(s) without fabricating unknown details.",
        affectedItemReferences=references,
        affectedItems=affected_items,
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


def _alternative(
    alternative_id: str,
    action: RecommendationAction,
    references: List[str],
    rationale: str,
    blocking: bool,
    constraints: List[str],
) -> AlternativeRecommendation:
    return AlternativeRecommendation(
        alternativeId=alternative_id,
        action=action,
        affectedItemReferences=references,
        rationale=rationale,
        safetyStatus=(
            SafetyStatus.MANUAL_REVIEW_REQUIRED
            if blocking or action != RecommendationAction.PROCEED_WITH_CAUTION
            else SafetyStatus.CONDITIONALLY_SAFE
        ),
        requiresHumanApproval=blocking or action != RecommendationAction.PROCEED_WITH_CAUTION,
        constraints=constraints,
    )


def build_bounded_alternatives(
    validation: TravelValidationInput,
) -> ToolExecutionResult:
    """Build at most three advisory alternatives from trusted issue types."""

    references = build_affected_item_list(validation)
    blocking = bool(identify_blocking_issues(validation))
    alternatives: List[AlternativeRecommendation] = []

    if any(issue.issue_type == ValidationIssueType.TRAVEL_ALERT for issue in validation.issues):
        critical = any(
            issue.issue_type == ValidationIssueType.TRAVEL_ALERT
            and issue.severity == RiskLevel.CRITICAL
            and issue.is_blocking
            for issue in validation.issues
        )
        alternatives.append(
            _alternative(
                "alternative_reschedule",
                RecommendationAction.RESCHEDULE,
                references,
                "Move affected activities away from the authoritative travel-alert window.",
                critical,
                ["Travel timing and alert conditions require human review."],
            )
        )

    if any(
        issue.issue_type in {
            ValidationIssueType.SCHEDULE_CONFLICT,
            ValidationIssueType.INVALID_TIME_RANGE,
        }
        for issue in validation.issues
    ):
        alternatives.append(
            _alternative(
                "alternative_schedule_review",
                RecommendationAction.RESOLVE_SCHEDULE_CONFLICT,
                references,
                "Adjust the affected activity timing without changing itinerary data automatically.",
                True,
                ["A human must confirm travel time and operating constraints."],
            )
        )

    if any(issue.issue_type == ValidationIssueType.BUDGET_EXCEEDED for issue in validation.issues):
        alternatives.append(
            _alternative(
                "alternative_budget_review",
                RecommendationAction.REVIEW_BUDGET,
                references,
                "Review costs or select a lower-cost itinerary option.",
                True,
                ["No prices or availability are changed by this recommendation."],
            )
        )

    if not alternatives and validation.issues:
        alternatives.append(
            _alternative(
                "alternative_manual_review",
                RecommendationAction.MANUAL_REVIEW,
                references,
                "Review the validation issue before proceeding.",
                True,
                ["The available context is insufficient for a more specific alternative."],
            )
        )

    # A blocking or infeasible validation can never receive Proceed here.
    alternatives = [
        alternative
        for alternative in alternatives[:3]
        if not (
            (blocking or not validation.is_feasible)
            and alternative.action == RecommendationAction.PROCEED
        )
    ]
    return ToolExecutionResult(
        toolName="build_recommendation_candidates",
        summary=f"Built {len(alternatives)} bounded alternative recommendation(s).",
        candidateActions=[alternative.action for alternative in alternatives],
        alternatives=alternatives,
    )


def _overlaps(start, end, other_start, other_end) -> bool:
    return start < other_end and end > other_start


def find_safe_time_windows(
    validation: TravelValidationInput,
) -> ToolExecutionResult:
    """Suggest only conditionally safe windows derivable from supplied context."""

    references = build_affected_item_list(validation)
    context_by_reference = {item.reference: item for item in validation.itinerary_items}
    valid_items = [
        item
        for item in validation.itinerary_items
        if item.start_date_time and item.end_date_time and item.end_date_time > item.start_date_time
    ]
    windows: List[SafeWindowSuggestion] = []

    for reference in references:
        item = context_by_reference.get(reference)
        if item is None or item.start_date_time is None or item.end_date_time is None:
            continue
        if item.end_date_time <= item.start_date_time:
            continue

        duration = item.end_date_time - item.start_date_time
        candidate_start = max(entry.end_date_time for entry in valid_items) + timedelta(minutes=30)
        candidate_end = candidate_start + duration
        constraints = [
            "Operating hours, travel duration, and availability were not supplied.",
            "Human review is required before changing the itinerary.",
        ]

        for _ in range(20):
            conflict = next(
                (
                    entry
                    for entry in valid_items
                    if entry.reference != reference
                    and entry.start_date_time
                    and entry.end_date_time
                    and _overlaps(candidate_start, candidate_end, entry.start_date_time, entry.end_date_time)
                ),
                None,
            )
            alert_conflict = next(
                (
                    alert
                    for alert in validation.blocking_travel_alert_windows
                    if item.district
                    and alert.district.lower() == item.district.lower()
                    and _overlaps(candidate_start, candidate_end, alert.start_date_time, alert.end_date_time)
                ),
                None,
            )
            if conflict:
                candidate_start = conflict.end_date_time + timedelta(minutes=30)
                candidate_end = candidate_start + duration
                continue
            if alert_conflict:
                candidate_start = alert_conflict.end_date_time + timedelta(minutes=30)
                candidate_end = candidate_start + duration
                continue
            break
        else:
            continue

        windows.append(
            SafeWindowSuggestion(
                itemReference=reference,
                proposedStart=candidate_start,
                proposedEnd=candidate_end,
                reason="Suggested after supplied itinerary items and outside known blocking alert windows.",
                safetyStatus=SafetyStatus.CONDITIONALLY_SAFE,
                constraints=constraints,
            )
        )

    return ToolExecutionResult(
        toolName="find_safe_time_windows",
        summary=(
            f"Produced {len(windows)} conditional time-window suggestion(s); "
            "missing context produces no fabricated window."
        ),
        safeWindows=windows,
    )


# This registry is intentionally explicit. Model/provider strings are looked up
# here and can only reach these read-only deterministic functions.
TOOL_REGISTRY = {
    "summarize_validation": summarize_validation_tool,
    "list_blocking_issues": list_blocking_issues_tool,
    "identify_affected_items": identify_affected_items_tool,
    "assess_travel_risk": assess_travel_risk_tool,
    "build_recommendation_candidates": build_bounded_alternatives,
    "find_safe_time_windows": find_safe_time_windows,
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
