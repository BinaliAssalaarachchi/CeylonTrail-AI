namespace CeylonTrail.Api.Models;

public class Trip
{
    public Guid Id { get; set; }

    public Guid TouristId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public decimal Budget { get; set; }

    public TripStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User Tourist { get; set; } = null!;

    public ICollection<TripPreference> Preferences { get; set; } = new List<TripPreference>();

    public ICollection<Itinerary> Itineraries { get; set; } = new List<Itinerary>();
}
