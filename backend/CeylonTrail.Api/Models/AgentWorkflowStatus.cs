namespace CeylonTrail.Api.Models;

public enum AgentWorkflowStatus
{
    Pending,
    Running,
    AwaitingApproval,
    Completed,
    FailedSafe,
    Cancelled
}
