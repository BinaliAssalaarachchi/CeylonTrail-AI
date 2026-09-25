using CeylonTrail.Api.DTOs.ApprovalRequests;

namespace CeylonTrail.Api.Interfaces;

public interface IItineraryTravelIntelligenceWorkflowService
{
    Task<ItineraryTravelIntelligenceWorkflowResult> ProcessAsync(
        Guid tripId,
        Guid touristId,
        CancellationToken cancellationToken = default);
}

public sealed record ItineraryTravelIntelligenceWorkflowResult(
    bool Succeeded,
    string? Error,
    Guid? ValidationResultId,
    Guid? ExecutionId,
    ApprovalRequestResponse? ApprovalRequest)
{
    public static ItineraryTravelIntelligenceWorkflowResult Failure(string error) =>
        new(false, error, null, null, null);
}
