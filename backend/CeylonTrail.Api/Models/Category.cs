namespace CeylonTrail.Api.Models;

public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Attraction> Attractions { get; set; } = new List<Attraction>();
}
