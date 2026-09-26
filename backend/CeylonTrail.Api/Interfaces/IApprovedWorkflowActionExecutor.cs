using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.Interfaces;

public sealed record ApprovedWorkflowActionExecutionResult(
    bool Succeeded,
    string? Error = null,
    Guid? BookingId = null,
    bool AlreadyExecuted = false);

public interface IApprovedWorkflowActionExecutor
{
    Task<ApprovedWorkflowActionExecutionResult> ExecuteAsync(
        ApprovalRequest approvalRequest,
        Guid decidedByUserId,
        CancellationToken cancellationToken = default);
}
