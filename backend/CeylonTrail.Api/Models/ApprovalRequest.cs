namespace CeylonTrail.Api.Models;

public class ApprovalRequest
{
    public Guid Id { get; set; }

    public Guid ValidationResultId { get; set; }

    public ValidationResult? ValidationResult { get; set; }

    public Guid? TravelIntelligenceExecutionId { get; set; }

    public TravelIntelligenceExecution? TravelIntelligenceExecution { get; set; }

    public Guid RequestedByUserId { get; set; }

    public User? RequestedByUser { get; set; }

    public ApprovalRequestStatus Status { get; set; } = ApprovalRequestStatus.Pending;

    public ApprovalRecommendedAction RecommendedAction { get; set; }

    public ValidationRiskLevel RiskLevel { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string? AffectedItemReferences { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ApprovalDecision? Decision { get; set; }
}
