namespace CeylonTrail.Api.DTOs.Trips;

public sealed record ItineraryDayResponse(
    Guid Id,
    int DayNumber,
    DateOnly Date,
    IReadOnlyList<ItineraryItemResponse> Items);
