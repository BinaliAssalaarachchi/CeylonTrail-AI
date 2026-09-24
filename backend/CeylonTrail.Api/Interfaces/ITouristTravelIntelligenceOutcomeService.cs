using CeylonTrail.Api.DTOs.TravelIntelligence;

namespace CeylonTrail.Api.Interfaces;

public interface ITouristTravelIntelligenceOutcomeService
{
    Task<TouristTravelIntelligenceOutcomeResponse?> GetLatestAsync(
        Guid touristId,
        Guid tripId,
        CancellationToken cancellationToken = default);
}
