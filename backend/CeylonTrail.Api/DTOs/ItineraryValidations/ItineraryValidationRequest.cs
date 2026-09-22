namespace CeylonTrail.Api.DTOs.ItineraryValidations;

public sealed class ItineraryValidationRequest
{
    public string? TripReference { get; set; }

    public decimal? Budget { get; set; }

    public decimal? EstimatedCost { get; set; }

    public List<ItineraryItemRequest> Items { get; set; } = new();
}

public sealed class ItineraryItemRequest
{
    public string Reference { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string District { get; set; } = string.Empty;

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }
}
