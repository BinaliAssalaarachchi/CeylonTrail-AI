using CeylonTrail.Api.Models;
using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.ApprovalRequests;

public sealed class ApprovalDecisionRequest
{
    [StringLength(1000)]
    public string? Comment { get; set; }
}

public sealed class ApprovalRequestResponse
{
    public Guid Id { get; set; }

    public Guid ValidationResultId { get; set; }

    public Guid? AgentWorkflowId { get; set; }

    public string? TripReference { get; set; }

    public Guid RequestedByUserId { get; set; }

    public ApprovalRequestStatus Status { get; set; }

    public ApprovalRecommendedAction RecommendedAction { get; set; }

    public ValidationRiskLevel RiskLevel { get; set; }

    public string Summary { get; set; } = string.Empty;

    public List<string> AffectedItemReferences { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool? ExecutionSucceeded { get; set; }

    public Guid? BookingId { get; set; }

    public string? ExecutionMessage { get; set; }

    public ApprovalDecisionResponse? Decision { get; set; }
}

public sealed class ApprovalDecisionResponse
{
    public Guid Id { get; set; }

    public Guid DecidedByUserId { get; set; }

    public ApprovalDecisionType Decision { get; set; }

    public string? Comment { get; set; }

    public DateTime DecidedAt { get; set; }
}
