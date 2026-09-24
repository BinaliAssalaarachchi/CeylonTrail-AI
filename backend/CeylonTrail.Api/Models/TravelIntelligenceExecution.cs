namespace CeylonTrail.Api.Models;

public class TravelIntelligenceExecution
{
    public Guid Id { get; set; }

    public string WorkflowId { get; set; } = string.Empty;

    public Guid ValidationResultId { get; set; }

    public ValidationResult? ValidationResult { get; set; }

    public Guid RequestedByUserId { get; set; }

    public User? RequestedByUser { get; set; }

    public string AgentName { get; set; } = string.Empty;

    public string AgentVersion { get; set; } = string.Empty;

    public string ObjectiveName { get; set; } = string.Empty;

    public string ObjectiveDescription { get; set; } = string.Empty;

    public string ObjectiveSource { get; set; } = string.Empty;

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

    public bool ToolSelectionProviderAttempted { get; set; }

    public string SelectedToolNamesJson { get; set; } = "[]";

    public string RejectedToolNamesJson { get; set; } = "[]";

    public bool ToolSelectionFallbackUsed { get; set; }

    public string? ToolSelectionFallbackReason { get; set; }

    public int SelectionAttemptCount { get; set; }

    public string RecommendationsJson { get; set; } = "[]";

    public string AffectedItemsJson { get; set; } = "[]";

    public string AlternativesJson { get; set; } = "[]";

    public string SafeWindowsJson { get; set; } = "[]";

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public int DurationMs { get; set; }

    public string ResultSummary { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public ICollection<TravelIntelligenceExecutionStep> Steps { get; set; } = new List<TravelIntelligenceExecutionStep>();

    public ApprovalRequest? ApprovalRequest { get; set; }
}
