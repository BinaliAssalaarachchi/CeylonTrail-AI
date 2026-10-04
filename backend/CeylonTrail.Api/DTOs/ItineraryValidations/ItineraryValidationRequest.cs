using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.ItineraryValidations;

public sealed class ItineraryValidationRequest
{
    [StringLength(200)]
    public string? TripReference { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? Budget { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal? EstimatedCost { get; set; }

    public List<ItineraryItemRequest> Items { get; set; } = new();
}

public sealed class ItineraryItemRequest
{
    [StringLength(200)]
    public string Reference { get; set; } = string.Empty;

    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(100)]
    public string District { get; set; } = string.Empty;

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }
}
