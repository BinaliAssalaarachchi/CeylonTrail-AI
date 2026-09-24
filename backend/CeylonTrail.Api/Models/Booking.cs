namespace CeylonTrail.Api.Models;

public class Booking
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid? TripId { get; set; }
    public Trip? Trip { get; set; }

    public BookingStatus CurrentStatus { get; set; } = BookingStatus.Draft;

    public decimal TotalAmount { get; set; }

    public string? QrCodeHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BookingItem> Items { get; set; } = new List<BookingItem>();
    public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();
    public ICollection<CancellationRequest> CancellationRequests { get; set; } = new List<CancellationRequest>();
}
