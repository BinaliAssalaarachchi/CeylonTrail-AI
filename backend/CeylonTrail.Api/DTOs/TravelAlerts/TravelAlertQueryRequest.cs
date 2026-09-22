using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.DTOs.TravelAlerts;

public class TravelAlertQueryRequest
{
    public string? Search { get; set; }

    public string? District { get; set; }

    public TravelAlertStatus? Status { get; set; }

    public TravelAlertSeverity? Severity { get; set; }

    public TravelAlertType? AlertType { get; set; }

    public string SortBy { get; set; } = "createdAt";

    public string SortDirection { get; set; } = "desc";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
