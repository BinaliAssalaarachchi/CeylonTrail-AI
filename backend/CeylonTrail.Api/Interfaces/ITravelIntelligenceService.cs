using CeylonTrail.Api.DTOs.ItineraryValidations;

namespace CeylonTrail.Api.Interfaces;

public interface ITravelIntelligenceService
{
    Task<TravelIntelligenceResponse> AnalyzeAsync(
        ItineraryValidationResponse validation,
        CancellationToken cancellationToken = default);
}
