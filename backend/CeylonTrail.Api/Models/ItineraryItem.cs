namespace CeylonTrail.Api.Models;

public class ItineraryItem
{
    public Guid Id { get; set; }

    public Guid ItineraryDayId { get; set; }

    public Guid AttractionId { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public decimal EstimatedCost { get; set; }

    public string? Notes { get; set; }

    public ItineraryDay ItineraryDay { get; set; } = null!;
}
