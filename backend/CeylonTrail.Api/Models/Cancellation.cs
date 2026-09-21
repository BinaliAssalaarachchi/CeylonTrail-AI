using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.Models;

public class Cancellation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // 1-to-1 link to the booking
    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }

    // Mandatory reason for cancellation
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    // User ID of who cancelled the booking
    public Guid CancelledBy { get; set; }

    // Timestamp when cancellation occurred
    public DateTime CancelledAt { get; set; } = DateTime.UtcNow;
}
