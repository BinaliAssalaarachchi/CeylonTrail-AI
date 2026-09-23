namespace CeylonTrail.Api.Models;

public class AttractionSchedule
{
    public Guid Id { get; set; }
    public Guid AttractionId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly? OpeningTime { get; set; }
    public TimeOnly? ClosingTime { get; set; }
    public bool IsClosed { get; set; }
    public Attraction Attraction { get; set; } = null!;
}
