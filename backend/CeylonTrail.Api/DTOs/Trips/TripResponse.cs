using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.DTOs.Trips;

public sealed record TripResponse(
    Guid Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal Budget,
    TripStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<TripPreferenceResponse> Preferences);
