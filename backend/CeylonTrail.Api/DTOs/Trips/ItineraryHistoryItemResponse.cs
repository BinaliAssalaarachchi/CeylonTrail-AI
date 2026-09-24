using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.DTOs.Trips;

public sealed record ItineraryHistoryItemResponse(
    Guid Id,
    ItineraryStatus Status,
    decimal TotalEstimatedCost,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int DayCount);
