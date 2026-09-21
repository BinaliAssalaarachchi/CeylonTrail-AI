namespace CeylonTrail.Api.Models;

public class Booking
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // The tourist who created this booking
    public Guid TouristId { get; set; }
    public User? Tourist { get; set; }

    // Optional link to an approved trip itinerary
    public Guid? TripId { get; set; }

    // Controlled workflow status (Pending, Confirmed, Completed, Rejected, Cancelled)
    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    // Total cost of all booking items
    public decimal TotalAmount { get; set; }

    // Audit timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<BookingItem> Items { get; set; } = new List<BookingItem>();
    public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();
    public Cancellation? Cancellation { get; set; }
}
