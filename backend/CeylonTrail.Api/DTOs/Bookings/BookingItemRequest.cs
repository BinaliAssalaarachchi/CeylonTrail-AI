using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.Bookings;

public class BookingItemRequest
{
    [Required]
    public Guid AvailabilitySlotId { get; set; }

    [Range(1, 100, ErrorMessage = "NumberOfGuests must be between 1 and 100.")]
    public int NumberOfGuests { get; set; }

    [Range(0.01, 100000.0, ErrorMessage = "UnitPrice must be greater than 0.")]
    public decimal UnitPrice { get; set; }
}
