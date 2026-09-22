using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.DTOs.TravelAlerts;

public class TravelAlertResponse
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public TravelAlertType AlertType { get; set; }

    public TravelAlertSeverity Severity { get; set; }

    public string District { get; set; } = string.Empty;

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }

    public TravelAlertStatus Status { get; set; }

    public string? Source { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public string? CreatedByUserName { get; set; }
}
