namespace CeylonTrail.Api.DTOs.Attractions;

public sealed record AttractionScheduleResponse(
    Guid Id,
    Guid AttractionId,
    DayOfWeek DayOfWeek,
    TimeOnly? OpeningTime,
    TimeOnly? ClosingTime,
    bool IsClosed);
