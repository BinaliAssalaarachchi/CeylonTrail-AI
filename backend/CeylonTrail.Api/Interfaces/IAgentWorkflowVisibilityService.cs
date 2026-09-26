using CeylonTrail.Api.DTOs.AgentWorkflows;

namespace CeylonTrail.Api.Interfaces;

public interface IAgentWorkflowVisibilityService
{
    Task<TouristAgentWorkflowResponse?> GetLatestForTouristAsync(
        Guid touristId,
        Guid tripId,
        CancellationToken cancellationToken = default);

    Task<StaffAgentWorkflowResponse?> GetForStaffAsync(
        Guid workflowId,
        CancellationToken cancellationToken = default);

    Task ApplyApprovalExecutionOutcomeAsync(
        Guid approvalRequestId,
        DTOs.ApprovalRequests.ApprovalRequestResponse response,
        CancellationToken cancellationToken = default);
}
