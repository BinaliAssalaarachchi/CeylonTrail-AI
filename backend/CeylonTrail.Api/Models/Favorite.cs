namespace CeylonTrail.Api.Models;

public class Favorite
{
    public Guid Id { get; set; }
    public Guid TouristId { get; set; }
    public Guid AttractionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public User Tourist { get; set; } = null!;
    public Attraction Attraction { get; set; } = null!;
}
