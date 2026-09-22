"""Controlled, deterministic tools used by the Travel Intelligence Agent."""

from collections import Counter
from typing import Dict, List

from .schemas import (
    RecommendationAction,
    RiskLevel,
    TravelValidationInput,
    ValidationIssueInput,
    ValidationIssueType,
)


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
