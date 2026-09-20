namespace CeylonTrail.Api.DTOs.Attractions;

public sealed record FavoriteResponse(
    Guid TouristId,
    Guid AttractionId,
    DateTime CreatedAt);
