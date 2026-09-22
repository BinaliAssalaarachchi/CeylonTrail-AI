using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.Bookings;

public class CreateBookingRequest
{
    public Guid? TripId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "A booking must contain at least one item.")]
    public List<BookingItemRequest> Items { get; set; } = new();
}
