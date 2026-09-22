namespace CeylonTrail.Api.DTOs.TravelAlerts;

public class TravelAlertPageResponse
{
    public IReadOnlyList<TravelAlertResponse> Items { get; set; } = Array.Empty<TravelAlertResponse>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalCount { get; set; }

    public int TotalPages { get; set; }
}
