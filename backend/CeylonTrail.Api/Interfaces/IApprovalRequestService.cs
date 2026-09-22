using CeylonTrail.Api.DTOs.ApprovalRequests;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.Interfaces;

public interface IApprovalRequestService
{
    Task<(bool Succeeded, string? Error, ApprovalRequestResponse? Response)> CreateOrReusePendingAsync(
        Guid validationResultId,
        Guid requestedByUserId,
        TravelIntelligenceResponse recommendation,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApprovalRequestResponse>> ListAsync(
        ApprovalRequestStatus? status,
        CancellationToken cancellationToken = default);

    Task<ApprovalRequestResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, ApprovalRequestResponse? Response)> DecideAsync(
        Guid id,
        Guid decidedByUserId,
        ApprovalDecisionType decision,
        string? comment,
        CancellationToken cancellationToken = default);
}
