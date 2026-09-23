namespace CeylonTrail.Api.Models;

public class Itinerary
{
    public Guid Id { get; set; }

    public Guid TripId { get; set; }

    public ItineraryStatus Status { get; set; }

    public decimal TotalEstimatedCost { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Trip Trip { get; set; } = null!;

    public ICollection<ItineraryDay> Days { get; set; } = new List<ItineraryDay>();
}
