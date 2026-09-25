namespace CeylonTrail.Api.Models;

public class AgentWorkflow
{
    public Guid Id { get; set; }

    public Guid WorkflowId { get; set; }

    public Guid TripId { get; set; }

    public Trip Trip { get; set; } = null!;

    public Guid RequestedByUserId { get; set; }

    public User RequestedByUser { get; set; } = null!;

    public AgentWorkflowStatus Status { get; set; } = AgentWorkflowStatus.Pending;

    public AgentWorkflowAgentRole? CurrentStage { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? FailureCode { get; set; }

    public string? FailureSummary { get; set; }

    public int RetryCount { get; set; }

    public ICollection<AgentWorkflowStage> Stages { get; set; } = new List<AgentWorkflowStage>();
}
