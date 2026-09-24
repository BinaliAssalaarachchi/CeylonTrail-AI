using CeylonTrail.Api.DTOs.ApprovalRequests;
using CeylonTrail.Api.DTOs.ItineraryValidations;

namespace CeylonTrail.Api.Interfaces;

public interface ITravelIntelligenceExecutionPersistenceService
{
    Task<TravelIntelligenceExecutionPersistenceResult> PersistAsync(
        ItineraryValidationResponse validation,
        Guid requestedByUserId,
        TravelIntelligenceResponse recommendation,
        CancellationToken cancellationToken = default);
}

public sealed record TravelIntelligenceExecutionPersistenceResult(
    bool Succeeded,
    string? Error,
    Guid? ExecutionId,
    ApprovalRequestResponse? ApprovalRequest)
{
    public static TravelIntelligenceExecutionPersistenceResult Failure(string error) =>
        new(false, error, null, null);
}
