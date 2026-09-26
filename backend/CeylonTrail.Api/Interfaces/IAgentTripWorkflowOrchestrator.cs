using CeylonTrail.Api.DTOs.Trips;

namespace CeylonTrail.Api.Interfaces;

public interface IAgentTripWorkflowOrchestrator
{
    Task<TripServiceResult<ItineraryResponse>> ExecuteAsync(
        Guid touristId,
        Guid tripId,
        CancellationToken cancellationToken = default);
}
