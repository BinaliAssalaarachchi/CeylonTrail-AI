namespace CeylonTrail.Api.DTOs.Trips;

public sealed record ItineraryItemResponse(
    Guid Id,
    Guid AttractionId,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal EstimatedCost,
    string? Notes);
