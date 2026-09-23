namespace CeylonTrail.Api.Models;

public class AttractionImage
{
    public Guid Id { get; set; }
    public Guid AttractionId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public Attraction Attraction { get; set; } = null!;
}
