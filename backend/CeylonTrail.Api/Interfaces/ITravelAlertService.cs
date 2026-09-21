using CeylonTrail.Api.DTOs.TravelAlerts;

namespace CeylonTrail.Api.Interfaces;

public interface ITravelAlertService
{
    Task<(bool Succeeded, string? Error, TravelAlertResponse? Response)> CreateAsync(
        CreateTravelAlertRequest request,
        Guid authenticatedUserId,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, TravelAlertResponse? Response)> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, TravelAlertPageResponse? Response)> QueryAsync(
        TravelAlertQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error, TravelAlertResponse? Response)> UpdateAsync(
        Guid id,
        UpdateTravelAlertRequest request,
        CancellationToken cancellationToken = default);

    Task<(bool Succeeded, string? Error)> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
