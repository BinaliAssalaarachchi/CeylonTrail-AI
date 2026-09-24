namespace CeylonTrail.Api.Models;

public class BookingStatusHistory
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }

    public BookingStatus PreviousStatus { get; set; }
    public BookingStatus NewStatus { get; set; }

    public Guid? ChangedByUserId { get; set; }
    public User? ChangedByUser { get; set; }

    public string? Reason { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
