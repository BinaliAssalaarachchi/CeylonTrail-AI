namespace CeylonTrail.Api.Models;

public class AgentWorkflowStage
{
    public Guid Id { get; set; }

    public Guid AgentWorkflowId { get; set; }

    public AgentWorkflow AgentWorkflow { get; set; } = null!;

    public AgentWorkflowAgentRole AgentRole { get; set; }

    public int Sequence { get; set; }

    public AgentWorkflowStageStatus Status { get; set; } = AgentWorkflowStageStatus.Pending;

    public int AttemptNumber { get; set; } = 1;

    public string? InputSnapshotJson { get; set; }

    public string? OutputSnapshotJson { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorSummary { get; set; }

    // These references remain nullable until the individual agent integrations are wired.
    public Guid? ValidationResultId { get; set; }

    public Guid? TravelIntelligenceExecutionId { get; set; }

    public Guid? ApprovalRequestId { get; set; }
}
