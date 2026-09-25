namespace CeylonTrail.Api.DTOs.Attractions;

public sealed record AttractionImageResponse(
    Guid Id,
    Guid AttractionId,
    string ImageUrl,
    string? AltText,
    int SortOrder,
    bool IsPrimary,
    DateTime CreatedAt);
