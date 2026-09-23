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
