using CeylonTrail.Api.DTOs.ApprovalRequests;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.DTOs.Planner;
using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.DTOs.TravelIntelligence;

public sealed class TravelIntelligenceExecutionQuery
{
    public Guid? ValidationResultId { get; set; }
    public string? ExecutionStatus { get; set; }
    public ValidationRiskLevel? RiskLevel { get; set; }
    public bool? RequiresHumanApproval { get; set; }
    public bool? UsedFallback { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class TravelIntelligenceExecutionPageResponse
{
    public IReadOnlyList<TravelIntelligenceExecutionListItemResponse> Items { get; set; } = Array.Empty<TravelIntelligenceExecutionListItemResponse>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class TravelIntelligenceExecutionListItemResponse
{
    public Guid ExecutionId { get; set; }
    public string WorkflowId { get; set; } = string.Empty;
    public Guid ValidationResultId { get; set; }
    public Guid? TripId { get; set; }
    public string? TripName { get; set; }
    public string? DestinationName { get; set; }
    public string AgentName { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public string ExecutionStatus { get; set; } = string.Empty;
    public ValidationRiskLevel RiskLevel { get; set; }
    public bool IsFeasible { get; set; }
    public ApprovalRecommendedAction RecommendedAction { get; set; }
    public string Summary { get; set; } = string.Empty;
    public bool RequiresHumanApproval { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderName { get; set; }
    public string? ModelName { get; set; }
    public bool ProviderAttempted { get; set; }
    public bool ProviderSucceeded { get; set; }
    public bool UsedFallback { get; set; }
    public string? FallbackReason { get; set; }
    public int? ProviderLatencyMs { get; set; }
    public int ProviderAttemptCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int DurationMs { get; set; }
    public ApprovalRequestResponse? Approval { get; set; }
}

public sealed class TravelIntelligenceExecutionDetailResponse : TravelIntelligenceExecutionListItemResponse
{
    public AgentExecutionTrace? SharedTrace { get; set; }
    public string ObjectiveName { get; set; } = string.Empty;
    public string ObjectiveDescription { get; set; } = string.Empty;
    public string ObjectiveSource { get; set; } = string.Empty;
    public string ResultSummary { get; set; } = string.Empty;
    public bool ToolSelectionProviderAttempted { get; set; }
    public List<string> SelectedToolNames { get; set; } = new();
    public List<string> RejectedToolNames { get; set; } = new();
    public bool ToolSelectionFallbackUsed { get; set; }
    public string? ToolSelectionFallbackReason { get; set; }
    public int SelectionAttemptCount { get; set; }
    public List<TravelIntelligenceRecommendation> Recommendations { get; set; } = new();
    public List<TravelIntelligenceAffectedItem> AffectedItems { get; set; } = new();
    public List<TravelIntelligenceAlternativeRecommendation> Alternatives { get; set; } = new();
    public List<TravelIntelligenceSafeWindowSuggestion> SafeWindows { get; set; } = new();
    public List<TravelIntelligenceExecutionStepResponse> Steps { get; set; } = new();
}

public sealed class TravelIntelligenceExecutionStepResponse
{
    public int Sequence { get; set; }
    public string StepId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string? PlannedToolName { get; set; }
    public string? ExecutedToolName { get; set; }
    public TravelIntelligenceExecutionStepStatus Status { get; set; }
    public int? DurationMs { get; set; }
    public string? ResultSummary { get; set; }
}
