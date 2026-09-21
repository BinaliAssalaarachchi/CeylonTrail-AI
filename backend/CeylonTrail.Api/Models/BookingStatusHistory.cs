namespace CeylonTrail.Api.Models;

public class BookingStatusHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Link to the parent booking
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }

    // Status transition tracking
    public BookingStatus PreviousStatus { get; set; }
    public BookingStatus NewStatus { get; set; }

    // User who changed the status (Tourist, Provider, Coordinator, or Admin)
    public Guid ChangedBy { get; set; }

    // Timestamp of change
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    // Reason or notes for status change (e.g. rejection or cancellation explanation)
    public string? Reason { get; set; }
}
