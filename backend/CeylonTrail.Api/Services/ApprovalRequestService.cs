using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.ApprovalRequests;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class ApprovalRequestService(ApplicationDbContext dbContext) : IApprovalRequestService
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

        return requests.Select(request => ToResponse(request)).ToList();
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

        return request is null ? null : ToResponse(request);
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
        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        var decidedRequest = await dbContext.ApprovalRequests
            .AsNoTracking()
            .Include(candidate => candidate.Decision)
            .Include(candidate => candidate.ValidationResult)
            .SingleAsync(candidate => candidate.Id == id, cancellationToken);

        return (true, null, ToResponse(decidedRequest));
    }

    private static ApprovalRecommendedAction ToModelAction(TravelIntelligenceAction action) =>
        (ApprovalRecommendedAction)action;

    private static ApprovalRequestResponse ToResponse(
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
            }
    };

    private static List<string> SplitReferences(string? references) =>
        (references ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
