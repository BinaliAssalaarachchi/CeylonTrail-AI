using CeylonTrail.Api.Models;
using CeylonTrail.Api.DTOs.ApprovalRequests;

namespace CeylonTrail.Api.DTOs.ItineraryValidations;

public sealed class TravelIntelligenceValidationRequest
{
    public Guid ValidationResultId { get; set; }

    public string? TripReference { get; set; }

    public ValidationOverallStatus OverallStatus { get; set; }

    public ValidationRiskLevel RiskLevel { get; set; }

    public bool IsFeasible { get; set; }

    public int TotalIssueCount { get; set; }

    public int BlockingIssueCount { get; set; }

    public List<TravelIntelligenceIssueRequest> Issues { get; set; } = new();

    public List<TravelIntelligenceItineraryItemRequest> ItineraryItems { get; set; } = new();

    public List<TravelIntelligenceTravelAlertWindow> BlockingTravelAlertWindows { get; set; } = new();
}

public sealed class TravelIntelligenceIssueRequest
{
    public ValidationIssueType IssueType { get; set; }

    public ValidationIssueSeverity Severity { get; set; }

    public string RuleCode { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public bool IsBlocking { get; set; }

    public string? RelatedDistrict { get; set; }

    public string? RelatedItemReference { get; set; }
}

public enum TravelIntelligenceAction
{
    Proceed,
    ProceedWithCaution,
    Reschedule,
    Reroute,
    ReviewBudget,
    ResolveScheduleConflict,
    ManualReview
}

public sealed class TravelIntelligenceResponse
{
    public string Summary { get; set; } = string.Empty;

    public ValidationRiskLevel RiskLevel { get; set; }

    public TravelIntelligenceAction RecommendedAction { get; set; }

    public List<TravelIntelligenceRecommendation> Recommendations { get; set; } = new();

    public bool RequiresHumanApproval { get; set; }

    public List<string> AffectedItemReferences { get; set; } = new();

    public List<TravelIntelligenceAffectedItem> AffectedItems { get; set; } = new();

    public List<TravelIntelligenceAlternativeRecommendation> Alternatives { get; set; } = new();

    public List<TravelIntelligenceSafeWindowSuggestion> SafeWindows { get; set; } = new();

    public Guid ValidationResultId { get; set; }

    public bool IsFeasible { get; set; }

    public TravelIntelligenceExecutionMetadata Execution { get; set; } = new();

    public ApprovalRequestResponse? ApprovalRequest { get; set; }
}

public sealed class TravelIntelligenceRecommendation
{
    public TravelIntelligenceAction Action { get; set; }

    public string Explanation { get; set; } = string.Empty;

    public List<string> AffectedItemReferences { get; set; } = new();
}

public sealed class TravelIntelligenceExecutionMetadata
{
    public string AgentName { get; set; } = string.Empty;

    public string AgentVersion { get; set; } = string.Empty;

    public Guid ValidationResultId { get; set; }

    public string Provider { get; set; } = string.Empty;

    public bool UsedFallback { get; set; }

    public string ExecutionStatus { get; set; } = string.Empty;

    public string? FallbackReason { get; set; }

    public Guid WorkflowId { get; set; }

    public TravelIntelligenceObjective Objective { get; set; } = new();

    public TravelIntelligenceInvestigationPlan InvestigationPlan { get; set; } = new();

    public List<TravelIntelligenceExecutedStep> ExecutedSteps { get; set; } = new();

    public string? ExecutedStepId { get; set; }

    public string? ExecutedToolName { get; set; }

    public int DurationMs { get; set; }

    public string ResultSummary { get; set; } = string.Empty;

    public string? ModelName { get; set; }

    public bool ProviderAttempted { get; set; }

    public bool ProviderSucceeded { get; set; }

    public string? ProviderName { get; set; }

    public int? ProviderLatencyMs { get; set; }

    public int ProviderAttemptCount { get; set; }

    public bool ToolSelectionProviderAttempted { get; set; }

    public List<string> SelectedToolNames { get; set; } = new();

    public List<string> RejectedToolNames { get; set; } = new();

    public bool ToolSelectionFallbackUsed { get; set; }

    public string? ToolSelectionFallbackReason { get; set; }

    public int SelectionAttemptCount { get; set; }
}

public sealed class TravelIntelligenceObjective
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;
}

public sealed class TravelIntelligenceInvestigationPlan
{
    public List<TravelIntelligenceInvestigationStep> Steps { get; set; } = new();
}

public sealed class TravelIntelligenceInvestigationStep
{
    public string StepId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string? ToolName { get; set; }

    public TravelIntelligenceStepStatus Status { get; set; }
}

public enum TravelIntelligenceStepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}

public sealed class TravelIntelligenceExecutedStep
{
    public string StepId { get; set; } = string.Empty;

    public string? ToolName { get; set; }

    public TravelIntelligenceStepStatus Status { get; set; }

    public int DurationMs { get; set; }

    public string ResultSummary { get; set; } = string.Empty;
}

public sealed class TravelIntelligenceItineraryItemRequest
{
    public string ItemReference { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? District { get; set; }

    public DateTime? StartDateTime { get; set; }

    public DateTime? EndDateTime { get; set; }

    public decimal? EstimatedCost { get; set; }
}

public sealed class TravelIntelligenceTravelAlertWindow
{
    public string District { get; set; } = string.Empty;

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }
}

public sealed class TravelIntelligenceAffectedItem
{
    public string ItemReference { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? District { get; set; }

    public DateTime? StartDateTime { get; set; }

    public DateTime? EndDateTime { get; set; }

    public decimal? EstimatedCost { get; set; }

    public List<ValidationIssueType> IssueTypes { get; set; } = new();

    public ValidationIssueSeverity? HighestIssueSeverity { get; set; }

    public bool IsBlocking { get; set; }

    public bool DetailsAvailable { get; set; }
}

public enum TravelIntelligenceSafetyStatus
{
    ConditionallySafe,
    ManualReviewRequired,
    NotAvailable
}

public sealed class TravelIntelligenceAlternativeRecommendation
{
    public string AlternativeId { get; set; } = string.Empty;

    public TravelIntelligenceAction Action { get; set; }

    public List<string> AffectedItemReferences { get; set; } = new();

    public string Rationale { get; set; } = string.Empty;

    public TravelIntelligenceSafetyStatus SafetyStatus { get; set; }

    public bool RequiresHumanApproval { get; set; }

    public List<string> Constraints { get; set; } = new();
}

public sealed class TravelIntelligenceSafeWindowSuggestion
{
    public string ItemReference { get; set; } = string.Empty;

    public DateTime ProposedStart { get; set; }

    public DateTime ProposedEnd { get; set; }

    public string Reason { get; set; } = string.Empty;

    public TravelIntelligenceSafetyStatus SafetyStatus { get; set; }

    public List<string> Constraints { get; set; } = new();
}
