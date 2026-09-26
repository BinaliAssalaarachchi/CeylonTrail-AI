namespace CeylonTrail.Api.Models;

public class Attraction
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "PendingApproval";
    public string? RejectionReason { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public User Provider { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public ICollection<AttractionSchedule> Schedules { get; set; } = new List<AttractionSchedule>();
    public ICollection<ExperienceSlot> ExperienceSlots { get; set; } = new List<ExperienceSlot>();
    public ICollection<AttractionImage> Images { get; set; } = new List<AttractionImage>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}
