using CeylonTrail.Api.DTOs.ItineraryValidations;

namespace CeylonTrail.Api.Interfaces;

public interface IItineraryValidationService
{
    Task<(bool Succeeded, string? Error, ItineraryValidationResponse? Response)> ValidateAsync(
        ItineraryValidationRequest request,
        Guid authenticatedUserId,
        CancellationToken cancellationToken = default);

    Task<ItineraryValidationResponse?> GetByIdAsync(
        Guid id,
        Guid authenticatedUserId,
        CancellationToken cancellationToken = default);
}
