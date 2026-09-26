using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.ApprovalRequests;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class ApprovalRequestService(
    ApplicationDbContext dbContext,
    IApprovedWorkflowActionExecutor? approvedWorkflowActionExecutor = null,
    IAgentWorkflowVisibilityService? workflowVisibilityService = null) : IApprovalRequestService
{
    public async Task<(bool Succeeded, string? Error, ApprovalRequestResponse? Response)> CreateOrReusePendingAsync(
        Guid validationResultId,
        Guid requestedByUserId,
        TravelIntelligenceResponse recommendation,
        CancellationToken cancellationToken = default)
    {
        var requesterExists = await dbContext.Users.AnyAsync(
            user => user.Id == requestedByUserId && user.IsActive,
            cancellationToken);
        if (!requesterExists)
        {
            return (false, "The authenticated requester does not exist or is inactive.", null);
        }

        var tripReference = await dbContext.ValidationResults
            .AsNoTracking()
            .Where(validation => validation.Id == validationResultId &&
                                 validation.CreatedByUserId == requestedByUserId)
            .Select(validation => validation.TripReference)
            .SingleOrDefaultAsync(cancellationToken);
        if (tripReference is null && !await dbContext.ValidationResults.AnyAsync(
                validation => validation.Id == validationResultId &&
                              validation.CreatedByUserId == requestedByUserId,
                cancellationToken))
        {
            return (false, "The itinerary validation was not found.", null);
        }

        var existing = await dbContext.ApprovalRequests
            .Include(request => request.Decision)
            .Include(request => request.ValidationResult)
            .SingleOrDefaultAsync(
                request => request.ValidationResultId == validationResultId &&
                           request.RequestedByUserId == requestedByUserId &&
                           request.Status == ApprovalRequestStatus.Pending,
                cancellationToken);
        if (existing is not null)
        {
            return (true, null, ToResponse(existing));
        }

        var now = DateTime.UtcNow;
        var request = new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            ValidationResultId = validationResultId,
            RequestedByUserId = requestedByUserId,
            Status = ApprovalRequestStatus.Pending,
            RecommendedAction = ToModelAction(recommendation.RecommendedAction),
            RiskLevel = recommendation.RiskLevel,
            Summary = recommendation.Summary,
            AffectedItemReferences = string.Join(", ", recommendation.AffectedItemReferences),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ApprovalRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (true, null, ToResponse(request, tripReference));
    }

    public async Task<IReadOnlyList<ApprovalRequestResponse>> ListAsync(
        ApprovalRequestStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ApprovalRequests
            .AsNoTracking()
            .Include(request => request.Decision)
            .Include(request => request.ValidationResult)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(request => request.Status == status.Value);
        }

        var requests = await query
            .OrderBy(request => request.Status == ApprovalRequestStatus.Pending ? 0 : 1)
            .ThenByDescending(request => request.CreatedAt)
            .ToListAsync(cancellationToken);

        var responses = new List<ApprovalRequestResponse>(requests.Count);
        foreach (var request in requests)
        {
            var response = ToResponse(request);
            if (workflowVisibilityService is not null)
            {
                await workflowVisibilityService.ApplyApprovalExecutionOutcomeAsync(request.Id, response, cancellationToken);
            }
            responses.Add(response);
        }

        return responses;
    }

    public async Task<ApprovalRequestResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var request = await dbContext.ApprovalRequests
            .AsNoTracking()
            .Include(candidate => candidate.Decision)
            .Include(candidate => candidate.ValidationResult)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (request is null)
        {
            return null;
        }

        var response = ToResponse(request);
        if (workflowVisibilityService is not null)
        {
            await workflowVisibilityService.ApplyApprovalExecutionOutcomeAsync(request.Id, response, cancellationToken);
        }

        return response;
    }

    public async Task<(bool Succeeded, string? Error, ApprovalRequestResponse? Response)> DecideAsync(
        Guid id,
        Guid decidedByUserId,
        ApprovalDecisionType decision,
        string? comment,
        CancellationToken cancellationToken = default)
    {
        var deciderExists = await dbContext.Users.AnyAsync(
            user => user.Id == decidedByUserId && user.IsActive,
            cancellationToken);
        if (!deciderExists)
        {
            return (false, "The authenticated decision maker does not exist or is inactive.", null);
        }

        var request = await dbContext.ApprovalRequests
            .Include(candidate => candidate.Decision)
            .Include(candidate => candidate.ValidationResult)
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (request is null)
        {
            return (false, "The approval request was not found.", null);
        }

        if (request.Status != ApprovalRequestStatus.Pending || request.Decision is not null)
        {
            if (request.Status == ApprovalRequestStatus.Approved &&
                request.Decision?.Decision == ApprovalDecisionType.Approved &&
                approvedWorkflowActionExecutor is not null)
            {
                var execution = await approvedWorkflowActionExecutor.ExecuteAsync(
                    request,
                    request.Decision.DecidedByUserId,
                    cancellationToken);
                var resumedResponse = ToResponse(request);
                resumedResponse.ExecutionSucceeded = execution.Succeeded;
                resumedResponse.BookingId = execution.BookingId;
                resumedResponse.ExecutionMessage = execution.Error ?? (execution.Succeeded
                    ? "Approved booking executed."
                    : "Approved decision recorded, but booking execution failed safely.");
                if (workflowVisibilityService is not null)
                {
                    await workflowVisibilityService.ApplyApprovalExecutionOutcomeAsync(id, resumedResponse, cancellationToken);
                }
                return (true, null, resumedResponse);
            }

            return (false, "The approval request has already been decided.", null);
        }

        var now = DateTime.UtcNow;
        request.Status = decision == ApprovalDecisionType.Approved
            ? ApprovalRequestStatus.Approved
            : ApprovalRequestStatus.Rejected;
        request.UpdatedAt = now;
        var decisionRecord = new ApprovalDecision
        {
            Id = Guid.NewGuid(),
            ApprovalRequestId = request.Id,
            DecidedByUserId = decidedByUserId,
            Decision = decision,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            DecidedAt = now
        };
        dbContext.ApprovalDecisions.Add(decisionRecord);

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            dbContext.ChangeTracker.Clear();
            return (false, "The approval request has already been decided.", null);
        }
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        var decidedRequest = await dbContext.ApprovalRequests
            .AsNoTracking()
            .Include(candidate => candidate.Decision)
            .Include(candidate => candidate.ValidationResult)
            .SingleAsync(candidate => candidate.Id == id, cancellationToken);

        var response = ToResponse(decidedRequest);
        if (decision == ApprovalDecisionType.Approved && approvedWorkflowActionExecutor is not null)
        {
            var execution = await approvedWorkflowActionExecutor.ExecuteAsync(
                decidedRequest,
                decidedByUserId,
                cancellationToken);
            response.ExecutionSucceeded = execution.Succeeded;
            response.BookingId = execution.BookingId;
            response.ExecutionMessage = execution.Error ?? (execution.Succeeded ? "Approved booking executed." : "Approved decision recorded, but booking execution failed safely.");
            if (workflowVisibilityService is not null)
            {
                await workflowVisibilityService.ApplyApprovalExecutionOutcomeAsync(id, response, cancellationToken);
            }
        }
        else if (decision == ApprovalDecisionType.Rejected)
        {
            await TerminalizeRejectedWorkflowAsync(id, cancellationToken);
        }

        return (true, null, response);
    }

    private static ApprovalRecommendedAction ToModelAction(TravelIntelligenceAction action) =>
        (ApprovalRecommendedAction)action;

    public static ApprovalRequestResponse ToResponseForRead(
        ApprovalRequest request,
        string? tripReference = null) => new()
    {
        Id = request.Id,
        ValidationResultId = request.ValidationResultId,
        TripReference = tripReference ?? request.ValidationResult?.TripReference,
        RequestedByUserId = request.RequestedByUserId,
        Status = request.Status,
        RecommendedAction = request.RecommendedAction,
        RiskLevel = request.RiskLevel,
        Summary = request.Summary,
        AffectedItemReferences = SplitReferences(request.AffectedItemReferences),
        CreatedAt = request.CreatedAt,
        UpdatedAt = request.UpdatedAt,
        Decision = request.Decision is null
            ? null
            : new ApprovalDecisionResponse
            {
                Id = request.Decision.Id,
                DecidedByUserId = request.Decision.DecidedByUserId,
                Decision = request.Decision.Decision,
                Comment = request.Decision.Comment,
                DecidedAt = request.Decision.DecidedAt
            },
        ExecutionSucceeded = null,
        BookingId = null,
        ExecutionMessage = null
        };

    private async Task TerminalizeRejectedWorkflowAsync(Guid approvalRequestId, CancellationToken cancellationToken)
    {
        var workflow = await dbContext.AgentWorkflows
            .Include(candidate => candidate.Stages)
            .SingleOrDefaultAsync(
                candidate => candidate.Stages.Any(stage => stage.ApprovalRequestId == approvalRequestId),
                cancellationToken);
        if (workflow is null || workflow.Status != AgentWorkflowStatus.AwaitingApproval)
        {
            return;
        }

        workflow.Status = AgentWorkflowStatus.Cancelled;
        workflow.CompletedAt = DateTime.UtcNow;
        workflow.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ApprovalRequestResponse ToResponse(ApprovalRequest request, string? tripReference = null) =>
        ToResponseForRead(request, tripReference);

    private static List<string> SplitReferences(string? references) =>
        (references ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
