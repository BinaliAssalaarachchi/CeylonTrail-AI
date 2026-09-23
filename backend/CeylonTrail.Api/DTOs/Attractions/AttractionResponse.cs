namespace CeylonTrail.Api.DTOs.Attractions;

public sealed record AttractionResponse(
    Guid Id,
    Guid ProviderId,
    Guid CategoryId,
    string Name,
    string Description,
    string District,
    string Address,
    decimal Latitude,
    decimal Longitude,
    decimal Price,
    string Status,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    CategoryResponse Category,
    IReadOnlyList<AttractionScheduleResponse> Schedules,
    IReadOnlyList<ExperienceSlotResponse> ExperienceSlots,
    IReadOnlyList<AttractionImageResponse> Images,
    bool IsFavorite);
