namespace CeylonTrail.Api.DTOs.Attractions;

public sealed record ExperienceSlotResponse(
    Guid Id,
    Guid AttractionId,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int Capacity,
    int AvailableCapacity);
