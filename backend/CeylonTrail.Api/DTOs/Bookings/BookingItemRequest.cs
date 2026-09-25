using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.Bookings;

public class BookingItemRequest
{
    [Required]
    public Guid AvailabilitySlotId { get; set; }

    [Range(1, 100, ErrorMessage = "NumberOfGuests must be between 1 and 100.")]
    public int NumberOfGuests { get; set; }

    // Accepted for backwards compatibility with the original M3 contract.
    // The server always uses AvailabilitySlot.PricePerPerson as the authoritative price.
    [Range(0.0, 100000.0, ErrorMessage = "UnitPrice must be non-negative when supplied.")]
    public decimal? UnitPrice { get; set; }
}
