namespace CeylonTrail.Api.Models;

public class ExperienceSlot
{
    public Guid Id { get; set; }
    public Guid AttractionId { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int Capacity { get; set; }
    public int AvailableCapacity { get; set; }
    public Attraction Attraction { get; set; } = null!;
}
