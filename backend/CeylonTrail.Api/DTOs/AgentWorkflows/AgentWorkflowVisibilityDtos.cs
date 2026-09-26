using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.DTOs.AgentWorkflows;

public sealed class AgentWorkflowStageSummaryResponse
{
    public int Sequence { get; set; }
    public AgentWorkflowAgentRole AgentRole { get; set; }
    public AgentWorkflowStageStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string Summary { get; set; } = string.Empty;
}

public sealed class AgentWorkflowBookingProposalResponse
{
    public Guid AttractionId { get; set; }
    public Guid AvailabilitySlotId { get; set; }
    public int GuestCount { get; set; }
    public decimal ProposedUnitPrice { get; set; }
    public decimal ProposedTotalPrice { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class TouristAgentWorkflowResponse
{
    public Guid WorkflowId { get; set; }
    public Guid TripId { get; set; }
    public AgentWorkflowStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool RequiresApproval { get; set; }
    public ApprovalRequestStatus? ReviewStatus { get; set; }
    public bool? ExecutionSucceeded { get; set; }
    public Guid? BookingId { get; set; }
    public string SafeMessage { get; set; } = string.Empty;
    public List<AgentWorkflowStageSummaryResponse> Stages { get; set; } = new();
}

public sealed class StaffAgentWorkflowResponse : TouristAgentWorkflowResponse
{
    public Guid RequestedByUserId { get; set; }
    public string? TouristName { get; set; }
    public string? TouristEmail { get; set; }
    public ValidationRiskLevel? RiskLevel { get; set; }
    public bool? IsFeasible { get; set; }
    public ApprovalRecommendedAction? RecommendedAction { get; set; }
    public Guid? ApprovalRequestId { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? ExecutionMessage { get; set; }
    public List<AgentWorkflowBookingProposalResponse> BookingProposals { get; set; } = new();
}
