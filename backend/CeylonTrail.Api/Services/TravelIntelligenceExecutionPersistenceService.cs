using System.Text.Json;
using System.Text.Json.Serialization;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.ApprovalRequests;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CeylonTrail.Api.Services;

public sealed class TravelIntelligenceExecutionPersistenceService(
    ApplicationDbContext dbContext,
    IApprovalRequestService approvalRequestService,
    ILogger<TravelIntelligenceExecutionPersistenceService> logger)
    : ITravelIntelligenceExecutionPersistenceService
{
    private const int MaxRecommendations = 20;
    private const int MaxExecutionSteps = 20;
    private const int MaxToolNames = 20;
    private const int MaxAffectedItems = 100;
    private const int MaxAlternatives = 3;
    private const int MaxSafeWindows = 20;

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<TravelIntelligenceExecutionPersistenceResult> PersistAsync(
        ItineraryValidationResponse validation,
        Guid requestedByUserId,
        TravelIntelligenceResponse recommendation,
        CancellationToken cancellationToken = default)
    {
        var boundsError = ValidateBounds(recommendation);
        if (boundsError is not null)
        {
            return TravelIntelligenceExecutionPersistenceResult.Failure(boundsError);
        }

        var metadata = recommendation.Execution;
        var workflowId = metadata.WorkflowId == Guid.Empty
            ? Guid.NewGuid().ToString()
            : metadata.WorkflowId.ToString();

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        try
        {
            var existing = await dbContext.TravelIntelligenceExecutions
                .Include(execution => execution.ApprovalRequest)
                .SingleOrDefaultAsync(
                    execution => execution.WorkflowId == workflowId,
                    cancellationToken);

            if (existing is not null)
            {
                if (existing.ValidationResultId != validation.Id ||
                    existing.RequestedByUserId != requestedByUserId)
                {
                    return TravelIntelligenceExecutionPersistenceResult.Failure(
                        "The workflow ID is already associated with a different validation or requester.");
                }

                var existingApproval = await EnsureExistingApprovalAsync(
                    existing,
                    validation,
                    requestedByUserId,
                    recommendation,
                    cancellationToken);

                if (existingApproval.Error is not null)
                {
                    return TravelIntelligenceExecutionPersistenceResult.Failure(existingApproval.Error);
                }

                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return new(
                    true,
                    null,
                    existing.Id,
                    existingApproval.Response);
            }

            var now = DateTime.UtcNow;
            var execution = MapExecution(
                validation,
                requestedByUserId,
                workflowId,
                recommendation,
                now);
            var steps = MapSteps(metadata, execution.Id);

            dbContext.TravelIntelligenceExecutions.Add(execution);
            dbContext.TravelIntelligenceExecutionSteps.AddRange(steps);
            await dbContext.SaveChangesAsync(cancellationToken);

            ApprovalRequestResponse? approvalResponse = null;
            if (recommendation.RequiresHumanApproval)
            {
                var approval = await approvalRequestService.CreateOrReusePendingAsync(
                    validation.Id,
                    requestedByUserId,
                    recommendation,
                    cancellationToken);
                if (!approval.Succeeded || approval.Response is null)
                {
                    return TravelIntelligenceExecutionPersistenceResult.Failure(
                        approval.Error ?? "The approval request could not be created.");
                }

                var approvalEntity = await dbContext.ApprovalRequests
                    .SingleAsync(request => request.Id == approval.Response.Id, cancellationToken);
                if (approvalEntity.Status == ApprovalRequestStatus.Pending)
                {
                    approvalEntity.TravelIntelligenceExecutionId = execution.Id;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                approvalResponse = approval.Response;
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return new(true, null, execution.Id, approvalResponse);
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            dbContext.ChangeTracker.Clear();
            var racedExecution = await dbContext.TravelIntelligenceExecutions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    execution => execution.WorkflowId == workflowId,
                    cancellationToken);
            if (racedExecution is not null &&
                racedExecution.ValidationResultId == validation.Id &&
                racedExecution.RequestedByUserId == requestedByUserId)
            {
                return new(true, null, racedExecution.Id, null);
            }

            logger.LogWarning(
                exception,
                "Travel Intelligence execution persistence encountered a unique-key conflict for workflow {WorkflowId}.",
                workflowId);
            return TravelIntelligenceExecutionPersistenceResult.Failure(
                "The Travel Intelligence execution could not be persisted due to a uniqueness conflict.");
        }
        catch (Exception exception)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            logger.LogError(
                exception,
                "Travel Intelligence execution persistence failed for validation {ValidationResultId}.",
                validation.Id);
            return TravelIntelligenceExecutionPersistenceResult.Failure(
                "Travel Intelligence execution could not be persisted.");
        }
    }

    private async Task<(string? Error, ApprovalRequestResponse? Response)> EnsureExistingApprovalAsync(
        TravelIntelligenceExecution existing,
        ItineraryValidationResponse validation,
        Guid requestedByUserId,
        TravelIntelligenceResponse recommendation,
        CancellationToken cancellationToken)
    {
        if (!recommendation.RequiresHumanApproval)
        {
            return (null, null);
        }

        if (existing.ApprovalRequest is not null)
        {
            return (
                null,
                await approvalRequestService.GetByIdAsync(
                    existing.ApprovalRequest.Id,
                    cancellationToken));
        }

        var approval = await approvalRequestService.CreateOrReusePendingAsync(
            validation.Id,
            requestedByUserId,
            recommendation,
            cancellationToken);
        if (!approval.Succeeded || approval.Response is null)
        {
            return (approval.Error ?? "The approval request could not be created.", null);
        }

        var approvalEntity = await dbContext.ApprovalRequests
            .SingleAsync(request => request.Id == approval.Response.Id, cancellationToken);
        if (approvalEntity.Status == ApprovalRequestStatus.Pending)
        {
            approvalEntity.TravelIntelligenceExecutionId = existing.Id;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return (null, approval.Response);
    }

    private static TravelIntelligenceExecution MapExecution(
        ItineraryValidationResponse validation,
        Guid requestedByUserId,
        string workflowId,
        TravelIntelligenceResponse recommendation,
        DateTime now) => new()
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            ValidationResultId = validation.Id,
            RequestedByUserId = requestedByUserId,
            AgentName = recommendation.Execution.AgentName,
            AgentVersion = recommendation.Execution.AgentVersion,
            ObjectiveName = recommendation.Execution.Objective.Name,
            ObjectiveDescription = recommendation.Execution.Objective.Description,
            ObjectiveSource = recommendation.Execution.Objective.Source,
            ExecutionStatus = recommendation.Execution.ExecutionStatus,
            RiskLevel = recommendation.RiskLevel,
            IsFeasible = recommendation.IsFeasible,
            RecommendedAction = (ApprovalRecommendedAction)recommendation.RecommendedAction,
            Summary = recommendation.Summary,
            RequiresHumanApproval = recommendation.RequiresHumanApproval,
            Provider = recommendation.Execution.Provider,
            ProviderName = recommendation.Execution.ProviderName,
            ModelName = recommendation.Execution.ModelName,
            ProviderAttempted = recommendation.Execution.ProviderAttempted,
            ProviderSucceeded = recommendation.Execution.ProviderSucceeded,
            UsedFallback = recommendation.Execution.UsedFallback,
            FallbackReason = SanitizeReason(recommendation.Execution.FallbackReason),
            ProviderLatencyMs = recommendation.Execution.ProviderLatencyMs,
            ProviderAttemptCount = recommendation.Execution.ProviderAttemptCount,
            ToolSelectionProviderAttempted = recommendation.Execution.ToolSelectionProviderAttempted,
            SelectedToolNamesJson = Serialize(recommendation.Execution.SelectedToolNames),
            RejectedToolNamesJson = Serialize(recommendation.Execution.RejectedToolNames),
            ToolSelectionFallbackUsed = recommendation.Execution.ToolSelectionFallbackUsed,
            ToolSelectionFallbackReason = SanitizeReason(recommendation.Execution.ToolSelectionFallbackReason),
            SelectionAttemptCount = recommendation.Execution.SelectionAttemptCount,
            RecommendationsJson = Serialize(recommendation.Recommendations),
            AffectedItemsJson = Serialize(recommendation.AffectedItems),
            AlternativesJson = Serialize(recommendation.Alternatives),
            SafeWindowsJson = Serialize(recommendation.SafeWindows),
            StartedAt = now,
            CompletedAt = now,
            DurationMs = recommendation.Execution.DurationMs,
            ResultSummary = recommendation.Execution.ResultSummary,
            CreatedAt = now
        };

    private static List<TravelIntelligenceExecutionStep> MapSteps(
        TravelIntelligenceExecutionMetadata metadata,
        Guid executionId)
    {
        var planSteps = metadata.InvestigationPlan.Steps;
        var executedById = metadata.ExecutedSteps
            .GroupBy(step => step.StepId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        if (executedById.Any(pair => pair.Value.Count > 1))
        {
            throw new InvalidOperationException("The execution contained duplicate step IDs.");
        }

        if (metadata.ExecutedSteps.Any(step => !planSteps.Any(plan => plan.StepId == step.StepId)))
        {
            throw new InvalidOperationException("The execution contained a step outside the investigation plan.");
        }

        return planSteps.Select((planStep, index) =>
        {
            executedById.TryGetValue(planStep.StepId, out var matches);
            var executed = matches?.SingleOrDefault();
            return new TravelIntelligenceExecutionStep
            {
                Id = Guid.NewGuid(),
                TravelIntelligenceExecutionId = executionId,
                Sequence = index + 1,
                StepId = planStep.StepId,
                Name = planStep.Name,
                Purpose = planStep.Purpose,
                PlannedToolName = planStep.ToolName,
                ExecutedToolName = executed?.ToolName,
                Status = executed is null
                    ? (TravelIntelligenceExecutionStepStatus)planStep.Status
                    : (TravelIntelligenceExecutionStepStatus)executed.Status,
                DurationMs = executed?.DurationMs,
                ResultSummary = executed?.ResultSummary,
                CreatedAt = DateTime.UtcNow
            };
        }).ToList();
    }

    private static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, SnapshotJsonOptions);

    private static string? SanitizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var normalized = new string(reason
            .Where(character => !char.IsControl(character))
            .ToArray())
            .Trim();
        if (normalized.Contains("api", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("bearer", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("prompt", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("payload", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("contents", StringComparison.OrdinalIgnoreCase))
        {
            return "Provider failure";
        }

        return normalized.Length <= 300 ? normalized : normalized[..300];
    }

    private static string? ValidateBounds(TravelIntelligenceResponse recommendation)
    {
        var execution = recommendation.Execution;
        if (recommendation.Recommendations.Count > MaxRecommendations ||
            recommendation.AffectedItems.Count > MaxAffectedItems ||
            recommendation.Alternatives.Count > MaxAlternatives ||
            recommendation.SafeWindows.Count > MaxSafeWindows ||
            execution.SelectedToolNames.Count > MaxToolNames ||
            execution.RejectedToolNames.Count > MaxToolNames ||
            execution.InvestigationPlan.Steps.Count > MaxExecutionSteps ||
            execution.ExecutedSteps.Count > MaxExecutionSteps)
        {
            return "Travel Intelligence execution metadata exceeded a safe persistence bound.";
        }

        if (recommendation.Summary.Length > 2000 ||
            execution.AgentName.Length > 150 ||
            execution.AgentVersion.Length > 50 ||
            execution.ExecutionStatus.Length > 30 ||
            execution.Provider.Length > 120 ||
            execution.ProviderName?.Length > 120 ||
            execution.ModelName?.Length > 120 ||
            execution.ResultSummary.Length > 500 ||
            execution.Objective.Name.Length > 100 ||
            execution.Objective.Description.Length > 500 ||
            execution.Objective.Source.Length > 50)
        {
            return "Travel Intelligence execution metadata exceeded a persistence field limit.";
        }

        return null;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
}
