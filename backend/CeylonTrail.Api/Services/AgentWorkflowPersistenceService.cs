using System.Text.Json;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class AgentWorkflowPersistenceService(ApplicationDbContext dbContext)
    : IAgentWorkflowPersistenceService
{
    private const int MaxFailureCodeLength = 100;
    private const int MaxFailureSummaryLength = 1000;
    private const int MaxSnapshotLength = 100_000;

    public async Task<AgentWorkflowPersistenceResult> CreateAsync(
        Guid workflowId,
        Guid tripId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        if (workflowId == Guid.Empty || tripId == Guid.Empty || requestedByUserId == Guid.Empty)
        {
            return Failure("Workflow, trip, and requester identifiers are required.");
        }

        var existing = await dbContext.AgentWorkflows
            .Include(workflow => workflow.Stages)
            .SingleOrDefaultAsync(workflow => workflow.WorkflowId == workflowId, cancellationToken);

        if (existing is not null)
        {
            return existing.TripId == tripId && existing.RequestedByUserId == requestedByUserId
                ? Success(existing)
                : Failure("WorkflowId is already associated with another trip or requester.");
        }

        var trip = await dbContext.Trips
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == tripId, cancellationToken);
        var requester = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(user => user.Id == requestedByUserId, cancellationToken);

        if (trip is null)
        {
            return Failure("Trip was not found.");
        }

        if (requester is null || !requester.IsActive)
        {
            return Failure("Requester was not found or is inactive.");
        }

        if (trip.TouristId != requestedByUserId)
        {
            return Failure("Requester does not own the trip.");
        }

        var now = DateTime.UtcNow;
        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            TripId = tripId,
            RequestedByUserId = requestedByUserId,
            Status = AgentWorkflowStatus.Pending,
            StartedAt = now,
            UpdatedAt = now,
            RetryCount = 0
        };

        dbContext.AgentWorkflows.Add(workflow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Success(workflow);
    }

    public async Task<AgentWorkflowPersistenceResult> GetByWorkflowIdAsync(
        Guid workflowId,
        Guid? requestedByUserId = null,
        CancellationToken cancellationToken = default)
    {
        var workflow = await dbContext.AgentWorkflows
            .Include(candidate => candidate.Stages.OrderBy(stage => stage.Sequence).ThenBy(stage => stage.AttemptNumber))
            .SingleOrDefaultAsync(candidate => candidate.WorkflowId == workflowId, cancellationToken);

        if (workflow is null || (requestedByUserId.HasValue && workflow.RequestedByUserId != requestedByUserId.Value))
        {
            return Failure("Workflow was not found.");
        }

        return Success(workflow);
    }

    public async Task<AgentWorkflowPersistenceResult> GetActiveForTripAsync(
        Guid tripId,
        Guid requestedByUserId,
        CancellationToken cancellationToken = default)
    {
        var workflow = await dbContext.AgentWorkflows
            .Include(candidate => candidate.Stages)
            .Where(candidate =>
                candidate.TripId == tripId &&
                candidate.RequestedByUserId == requestedByUserId &&
                (candidate.Status == AgentWorkflowStatus.Pending ||
                 candidate.Status == AgentWorkflowStatus.Running ||
                 candidate.Status == AgentWorkflowStatus.AwaitingApproval))
            .OrderByDescending(candidate => candidate.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return workflow is null ? Failure("No active workflow was found.") : Success(workflow);
    }

    public async Task<AgentWorkflowPersistenceResult> StartStageAsync(
        Guid workflowId,
        AgentWorkflowAgentRole agentRole,
        int sequence,
        int attemptNumber = 1,
        string? inputSnapshotJson = null,
        CancellationToken cancellationToken = default)
    {
        if (sequence < 1 || attemptNumber < 1)
        {
            return Failure("Stage sequence and attempt number must be positive.");
        }

        var snapshotError = ValidateSnapshot(inputSnapshotJson);
        if (snapshotError is not null)
        {
            return Failure(snapshotError);
        }

        var workflow = await LoadWorkflowAsync(workflowId, cancellationToken);
        if (workflow is null)
        {
            return Failure("Workflow was not found.");
        }

        if (IsTerminal(workflow.Status))
        {
            return Failure("A stage cannot be started for a terminal workflow.");
        }

        if (workflow.Stages.Any(stage => stage.AgentRole == agentRole && stage.AttemptNumber == attemptNumber))
        {
            return Failure("That agent stage attempt already exists.");
        }

        var now = DateTime.UtcNow;
        var stage = new AgentWorkflowStage
        {
            Id = Guid.NewGuid(),
            AgentWorkflowId = workflow.Id,
            AgentRole = agentRole,
            Sequence = sequence,
            AttemptNumber = attemptNumber,
            Status = AgentWorkflowStageStatus.Running,
            InputSnapshotJson = inputSnapshotJson,
            StartedAt = now
        };

        workflow.Status = AgentWorkflowStatus.Running;
        workflow.CurrentStage = agentRole;
        workflow.UpdatedAt = now;
        if (attemptNumber > 1)
        {
            workflow.RetryCount++;
        }

        dbContext.AgentWorkflowStages.Add(stage);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Success(workflow, stage);
    }

    public async Task<AgentWorkflowPersistenceResult> CompleteStageAsync(
        Guid workflowId,
        Guid stageId,
        string? outputSnapshotJson = null,
        Guid? validationResultId = null,
        Guid? travelIntelligenceExecutionId = null,
        Guid? approvalRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var snapshotError = ValidateSnapshot(outputSnapshotJson);
        if (snapshotError is not null)
        {
            return Failure(snapshotError);
        }

        var workflow = await LoadWorkflowAsync(workflowId, cancellationToken);
        var stage = workflow?.Stages.SingleOrDefault(candidate => candidate.Id == stageId);
        if (workflow is null || stage is null)
        {
            return Failure("Workflow stage was not found.");
        }

        if (stage.Status != AgentWorkflowStageStatus.Running)
        {
            return Failure("Only a running stage can be completed.");
        }

        var now = DateTime.UtcNow;
        stage.Status = AgentWorkflowStageStatus.Completed;
        stage.OutputSnapshotJson = outputSnapshotJson;
        stage.ValidationResultId = validationResultId;
        stage.TravelIntelligenceExecutionId = travelIntelligenceExecutionId;
        stage.ApprovalRequestId = approvalRequestId;
        stage.CompletedAt = now;
        workflow.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Success(workflow, stage);
    }

    public async Task<AgentWorkflowPersistenceResult> FailStageAsync(
        Guid workflowId,
        Guid stageId,
        string errorCode,
        string errorSummary,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(errorCode) || string.IsNullOrWhiteSpace(errorSummary))
        {
            return Failure("Stage failure code and summary are required.");
        }

        var workflow = await LoadWorkflowAsync(workflowId, cancellationToken);
        var stage = workflow?.Stages.SingleOrDefault(candidate => candidate.Id == stageId);
        if (workflow is null || stage is null)
        {
            return Failure("Workflow stage was not found.");
        }

        if (stage.Status != AgentWorkflowStageStatus.Running)
        {
            return Failure("Only a running stage can fail.");
        }

        var now = DateTime.UtcNow;
        stage.Status = AgentWorkflowStageStatus.Failed;
        stage.ErrorCode = Truncate(errorCode, MaxFailureCodeLength);
        stage.ErrorSummary = Truncate(errorSummary, MaxFailureSummaryLength);
        stage.CompletedAt = now;
        workflow.Status = AgentWorkflowStatus.FailedSafe;
        workflow.FailureCode = stage.ErrorCode;
        workflow.FailureSummary = stage.ErrorSummary;
        workflow.CompletedAt = now;
        workflow.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Success(workflow, stage);
    }

    public async Task<AgentWorkflowPersistenceResult> TransitionAsync(
        Guid workflowId,
        AgentWorkflowStatus status,
        AgentWorkflowAgentRole? currentStage = null,
        string? failureCode = null,
        string? failureSummary = null,
        CancellationToken cancellationToken = default)
    {
        var workflow = await LoadWorkflowAsync(workflowId, cancellationToken);
        if (workflow is null)
        {
            return Failure("Workflow was not found.");
        }

        if (!IsAllowedTransition(workflow.Status, status))
        {
            return Failure($"Transition from {workflow.Status} to {status} is not allowed.");
        }

        if (status == AgentWorkflowStatus.FailedSafe &&
            (string.IsNullOrWhiteSpace(failureCode) || string.IsNullOrWhiteSpace(failureSummary)))
        {
            return Failure("FailedSafe requires a failure code and summary.");
        }

        var now = DateTime.UtcNow;
        workflow.Status = status;
        workflow.CurrentStage = currentStage;
        workflow.UpdatedAt = now;

        if (IsTerminal(status))
        {
            workflow.CompletedAt ??= now;
        }
        else
        {
            workflow.CompletedAt = null;
        }

        if (status == AgentWorkflowStatus.FailedSafe)
        {
            workflow.FailureCode = Truncate(failureCode!, MaxFailureCodeLength);
            workflow.FailureSummary = Truncate(failureSummary!, MaxFailureSummaryLength);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Success(workflow);
    }

    private async Task<AgentWorkflow?> LoadWorkflowAsync(Guid workflowId, CancellationToken cancellationToken) =>
        await dbContext.AgentWorkflows
            .Include(workflow => workflow.Stages)
            .SingleOrDefaultAsync(workflow => workflow.WorkflowId == workflowId, cancellationToken);

    private static bool IsTerminal(AgentWorkflowStatus status) =>
        status is AgentWorkflowStatus.Completed or AgentWorkflowStatus.FailedSafe or AgentWorkflowStatus.Cancelled;

    private static bool IsAllowedTransition(AgentWorkflowStatus current, AgentWorkflowStatus next) =>
        current == next ||
        (current, next) switch
        {
            (AgentWorkflowStatus.Pending, AgentWorkflowStatus.Running) => true,
            (AgentWorkflowStatus.Running, AgentWorkflowStatus.AwaitingApproval) => true,
            (AgentWorkflowStatus.Running, AgentWorkflowStatus.Completed) => true,
            (AgentWorkflowStatus.Running, AgentWorkflowStatus.FailedSafe) => true,
            (AgentWorkflowStatus.Running, AgentWorkflowStatus.Cancelled) => true,
            (AgentWorkflowStatus.AwaitingApproval, AgentWorkflowStatus.Completed) => true,
            (AgentWorkflowStatus.AwaitingApproval, AgentWorkflowStatus.FailedSafe) => true,
            (AgentWorkflowStatus.AwaitingApproval, AgentWorkflowStatus.Cancelled) => true,
            _ => false
        };

    private static string? ValidateSnapshot(string? snapshotJson)
    {
        if (snapshotJson is null)
        {
            return null;
        }

        if (snapshotJson.Length > MaxSnapshotLength)
        {
            return "Workflow snapshots must not exceed 100000 characters.";
        }

        try
        {
            using var document = JsonDocument.Parse(snapshotJson);
            return null;
        }
        catch (JsonException)
        {
            return "Workflow snapshots must contain valid JSON.";
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static AgentWorkflowPersistenceResult Success(AgentWorkflow workflow, AgentWorkflowStage? stage = null) =>
        new(true, null, workflow, stage);

    private static AgentWorkflowPersistenceResult Failure(string error) =>
        new(false, error);
}
