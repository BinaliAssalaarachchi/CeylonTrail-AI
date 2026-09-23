using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.DTOs.Trips;

public sealed record ItineraryResponse(
    Guid Id,
    Guid TripId,
    ItineraryStatus Status,
    decimal TotalEstimatedCost,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ItineraryDayResponse> Days);
