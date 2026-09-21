using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.DTOs.Trips;

public sealed record StaffTripResponse(
    Guid Id,
    Guid TouristId,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal Budget,
    TripStatus Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool HasItinerary);
