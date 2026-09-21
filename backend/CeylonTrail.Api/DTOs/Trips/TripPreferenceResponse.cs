namespace CeylonTrail.Api.DTOs.Trips;

public sealed record TripPreferenceResponse(
    Guid Id,
    string PreferenceType,
    string Value);
