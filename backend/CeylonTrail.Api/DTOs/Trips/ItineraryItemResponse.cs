namespace CeylonTrail.Api.DTOs.Trips;

public sealed record ItineraryItemResponse(
    Guid Id,
    Guid AttractionId,
    TimeOnly StartTime,
    TimeOnly EndTime,
    decimal EstimatedCost,
    string? Notes,
    string? AttractionName = null,
    string? Description = null,
    string? Address = null,
    string? District = null,
    string? Category = null,
    decimal? Latitude = null,
    decimal? Longitude = null);
