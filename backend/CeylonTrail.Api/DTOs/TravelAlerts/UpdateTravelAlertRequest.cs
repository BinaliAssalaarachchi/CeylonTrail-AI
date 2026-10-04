using CeylonTrail.Api.Models;

using System.ComponentModel.DataAnnotations;

namespace CeylonTrail.Api.DTOs.TravelAlerts;

public class UpdateTravelAlertRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;

    public TravelAlertType AlertType { get; set; }

    public TravelAlertSeverity Severity { get; set; }

    [Required, StringLength(100, MinimumLength = 1)]
    public string District { get; set; } = string.Empty;

    public DateTime StartDateTime { get; set; }

    public DateTime EndDateTime { get; set; }

    public TravelAlertStatus Status { get; set; }

    [StringLength(500)]
    public string? Source { get; set; }
}
