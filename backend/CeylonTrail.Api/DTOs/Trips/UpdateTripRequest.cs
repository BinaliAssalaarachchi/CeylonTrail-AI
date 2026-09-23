using System.ComponentModel.DataAnnotations;
using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.DTOs.Trips;

public sealed class UpdateTripRequest
{
    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal Budget { get; set; }

    public TripStatus? Status { get; set; }
}
