using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.Interfaces;

public sealed record AgentWorkflowPersistenceResult(
    bool Succeeded,
    string? Error,
    AgentWorkflow? Workflow = null,
    AgentWorkflowStage? Stage = null);

public interface IAgentWorkflowPersistenceService
{
    Task<AgentWorkflowPersistenceResult> CreateAsync(
        Guid workflowId,
        Guid tripId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowPersistenceResult> GetByWorkflowIdAsync(
        Guid workflowId,
        Guid? requestedByUserId = null,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowPersistenceResult> StartStageAsync(
        Guid workflowId,
        AgentWorkflowAgentRole agentRole,
        int sequence,
        int attemptNumber = 1,
        string? inputSnapshotJson = null,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowPersistenceResult> CompleteStageAsync(
        Guid workflowId,
        Guid stageId,
        string? outputSnapshotJson = null,
        Guid? validationResultId = null,
        Guid? travelIntelligenceExecutionId = null,
        Guid? approvalRequestId = null,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowPersistenceResult> FailStageAsync(
        Guid workflowId,
        Guid stageId,
        string errorCode,
        string errorSummary,
        CancellationToken cancellationToken = default);

    Task<AgentWorkflowPersistenceResult> TransitionAsync(
        Guid workflowId,
        AgentWorkflowStatus status,
        AgentWorkflowAgentRole? currentStage = null,
        string? failureCode = null,
        string? failureSummary = null,
        CancellationToken cancellationToken = default);
}
