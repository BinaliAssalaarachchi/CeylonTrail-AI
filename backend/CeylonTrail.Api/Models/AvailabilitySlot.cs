using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.Models;

public class AvailabilitySlot
{
    public Guid Id { get; set; }

    public Guid AttractionId { get; set; }
    public Attraction? Attraction { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public int MaxCapacity { get; set; }
    public int BookedCapacity { get; set; }

    public int AvailableCapacity => Math.Max(0, MaxCapacity - BookedCapacity);

    public decimal PricePerPerson { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BookingItem> BookingItems { get; set; } = new List<BookingItem>();
}
