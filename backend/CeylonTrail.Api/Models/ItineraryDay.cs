namespace CeylonTrail.Api.Models;

public class ItineraryDay
{
    public Guid Id { get; set; }

    public Guid ItineraryId { get; set; }

    public int DayNumber { get; set; }

    public DateOnly Date { get; set; }

    public Itinerary Itinerary { get; set; } = null!;

    public ICollection<ItineraryItem> Items { get; set; } = new List<ItineraryItem>();
}
