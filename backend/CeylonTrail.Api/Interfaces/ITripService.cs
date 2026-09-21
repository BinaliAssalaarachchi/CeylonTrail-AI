using CeylonTrail.Api.DTOs.Trips;

namespace CeylonTrail.Api.Interfaces;

public sealed record TripServiceResult<T>(
    T? Value = default,
    string? Error = null,
    bool NotFound = false);

public interface ITripService
{
    Task<TripServiceResult<TripResponse>> CreateTripAsync(
        Guid touristId,
        CreateTripRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TripResponse>> GetTripsAsync(
        Guid touristId,
        CancellationToken cancellationToken = default);

    Task<TripServiceResult<TripResponse>> GetTripAsync(
        Guid touristId,
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<TripServiceResult<TripResponse>> UpdateTripAsync(
        Guid touristId,
        Guid tripId,
        UpdateTripRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteTripAsync(
        Guid touristId,
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<TripServiceResult<TripPreferenceResponse>> AddPreferenceAsync(
        Guid touristId,
        Guid tripId,
        AddTripPreferenceRequest request,
        CancellationToken cancellationToken = default);

    Task<TripServiceResult<ItineraryResponse>> GetLatestItineraryAsync(
        Guid touristId,
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StaffTripResponse>> GetStaffTripsAsync(
        CancellationToken cancellationToken = default);

    Task<TripServiceResult<StaffTripResponse>> GetStaffTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<TripServiceResult<ItineraryResponse>> GetStaffItineraryAsync(
        Guid tripId,
        CancellationToken cancellationToken = default);
}
