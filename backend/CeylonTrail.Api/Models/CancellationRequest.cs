namespace CeylonTrail.Api.Models;

public class CancellationRequest
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }
    public Booking? Booking { get; set; }

    public string Reason { get; set; } = string.Empty;

    public CancellationRequestStatus Status { get; set; } = CancellationRequestStatus.Pending;

    public decimal? RefundAmount { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public User? ReviewedByUser { get; set; }
}
