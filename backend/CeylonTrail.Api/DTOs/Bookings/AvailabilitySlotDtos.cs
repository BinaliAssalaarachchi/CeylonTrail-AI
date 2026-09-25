using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.Bookings;

public class CreateAvailabilitySlotRequest
{
    [Required]
    public Guid AttractionId { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    [Range(1, 10000, ErrorMessage = "MaxCapacity must be at least 1.")]
    public int MaxCapacity { get; set; }

    [Range(0.0, 100000.0, ErrorMessage = "PricePerPerson must be non-negative.")]
    public decimal PricePerPerson { get; set; }
}

public record AvailabilitySlotResponse(
    Guid Id,
    Guid AttractionId,
    DateTime StartTime,
    DateTime EndTime,
    int MaxCapacity,
    int BookedCapacity,
    int AvailableCapacity,
    decimal PricePerPerson
);
