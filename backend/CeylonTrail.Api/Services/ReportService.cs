using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Reports;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class ReportService(ApplicationDbContext dbContext) : IReportService
{
    public async Task<ReportOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var tripGroups = await dbContext.Trips
            .AsNoTracking()
            .GroupBy(trip => trip.Status)
            .Select(group => new StatusCount<TripStatus>(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        var bookingGroups = await dbContext.Bookings
            .AsNoTracking()
            .GroupBy(booking => booking.CurrentStatus)
            .Select(group => new StatusCount<BookingStatus>(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        var approvalGroups = await dbContext.ApprovalRequests
            .AsNoTracking()
            .GroupBy(request => request.Status)
            .Select(group => new StatusCount<ApprovalRequestStatus>(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        var activeAlertGroups = await dbContext.TravelAlerts
            .AsNoTracking()
            .Where(alert => alert.Status == TravelAlertStatus.Active)
            .GroupBy(alert => alert.Severity)
            .Select(group => new StatusCount<TravelAlertSeverity>(group.Key, group.Count()))
            .ToListAsync(cancellationToken);
        var workflowGroups = await dbContext.AgentWorkflows
            .AsNoTracking()
            .GroupBy(workflow => workflow.Status)
            .Select(group => new StatusCount<AgentWorkflowStatus>(group.Key, group.Count()))
            .ToListAsync(cancellationToken);

        return new ReportOverviewResponse
        {
            TotalTrips = tripGroups.Sum(group => group.Count),
            TripsByStatus = ToDictionary(tripGroups),
            TotalBookings = bookingGroups.Sum(group => group.Count),
            BookingsByStatus = ToDictionary(bookingGroups),
            TotalApprovalRequests = approvalGroups.Sum(group => group.Count),
            ApprovalRequestsByStatus = ToDictionary(approvalGroups),
            PendingApprovalRequests = Count(approvalGroups, ApprovalRequestStatus.Pending),
            ActiveTravelAlerts = activeAlertGroups.Sum(group => group.Count),
            ActiveAlertsBySeverity = ToDictionary(activeAlertGroups),
            TotalWorkflows = workflowGroups.Sum(group => group.Count),
            WorkflowsByStatus = ToDictionary(workflowGroups),
            AwaitingApprovalWorkflows = Count(workflowGroups, AgentWorkflowStatus.AwaitingApproval),
            CompletedWorkflows = Count(workflowGroups, AgentWorkflowStatus.Completed),
            FailedSafeWorkflows = Count(workflowGroups, AgentWorkflowStatus.FailedSafe)
        };
    }

    private static Dictionary<string, int> ToDictionary<T>(IEnumerable<StatusCount<T>> groups)
        where T : struct, Enum =>
        groups.ToDictionary(group => group.Status.ToString(), group => group.Count, StringComparer.Ordinal);

    private static int Count<T>(IEnumerable<StatusCount<T>> groups, T status)
        where T : struct, Enum =>
        groups.FirstOrDefault(group => EqualityComparer<T>.Default.Equals(group.Status, status))?.Count ?? 0;

    private sealed record StatusCount<T>(T Status, int Count) where T : struct, Enum;
}
