namespace CeylonTrail.Api.DTOs.Reports;

public sealed class ReportOverviewResponse
{
    public int TotalTrips { get; set; }
    public Dictionary<string, int> TripsByStatus { get; set; } = new(StringComparer.Ordinal);
    public int TotalBookings { get; set; }
    public Dictionary<string, int> BookingsByStatus { get; set; } = new(StringComparer.Ordinal);
    public int TotalApprovalRequests { get; set; }
    public Dictionary<string, int> ApprovalRequestsByStatus { get; set; } = new(StringComparer.Ordinal);
    public int PendingApprovalRequests { get; set; }
    public int ActiveTravelAlerts { get; set; }
    public Dictionary<string, int> ActiveAlertsBySeverity { get; set; } = new(StringComparer.Ordinal);
    public int TotalWorkflows { get; set; }
    public Dictionary<string, int> WorkflowsByStatus { get; set; } = new(StringComparer.Ordinal);
    public int AwaitingApprovalWorkflows { get; set; }
    public int CompletedWorkflows { get; set; }
    public int FailedSafeWorkflows { get; set; }
}
