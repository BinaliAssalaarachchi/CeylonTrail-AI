namespace CeylonTrail.Api.DTOs.Attractions;

public sealed record AvailabilityResponse(
    Guid AttractionId,
    DateOnly? Date,
    IReadOnlyList<ExperienceSlotResponse> Slots);
