using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.DTOs.TravelAlerts;

public class CreateTravelAlertRequest
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public TravelAlertType AlertType { get; set; }

    public TravelAlertSeverity Severity { get; set; }

    public string District { get; set; } = string.Empty;

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }

    public TravelAlertStatus Status { get; set; }

    public string? Source { get; set; }
}
