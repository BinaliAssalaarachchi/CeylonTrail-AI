using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.Bookings;

public class RejectBookingRequest
{
    [Required(ErrorMessage = "Rejection reason is required.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Reason must be between 3 and 500 characters.")]
    public string Reason { get; set; } = string.Empty;
}
