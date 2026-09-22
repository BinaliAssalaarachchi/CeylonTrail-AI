using System.Security.Claims;
using CeylonTrail.Api.DTOs.ApprovalRequests;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CeylonTrail.Api.Controllers;

[ApiController]
[Route("api/approval-requests")]
[Authorize(Roles = "TravelCoordinator,Administrator")]
public sealed class ApprovalRequestsController(IApprovalRequestService approvalRequestService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApprovalRequestResponse>>> List(
        [FromQuery] ApprovalRequestStatus? status,
        CancellationToken cancellationToken)
    {
        return Ok(await approvalRequestService.ListAsync(status, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApprovalRequestResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await approvalRequestService.GetByIdAsync(id, cancellationToken);
        return result is null
            ? NotFound(new { message = "The approval request was not found." })
            : Ok(result);
    }

    [HttpPost("{id:guid}/approve")]
    public Task<ActionResult<ApprovalRequestResponse>> Approve(
        Guid id,
        ApprovalDecisionRequest? request,
        CancellationToken cancellationToken) =>
        Decide(id, ApprovalDecisionType.Approved, request?.Comment, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    public Task<ActionResult<ApprovalRequestResponse>> Reject(
        Guid id,
        ApprovalDecisionRequest? request,
        CancellationToken cancellationToken) =>
        Decide(id, ApprovalDecisionType.Rejected, request?.Comment, cancellationToken);

    private async Task<ActionResult<ApprovalRequestResponse>> Decide(
        Guid id,
        ApprovalDecisionType decision,
        string? comment,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized(new { message = "The authenticated user ID is missing or invalid." });
        }

        var result = await approvalRequestService.DecideAsync(
            id,
            userId,
            decision,
            comment,
            cancellationToken);
        if (result.Succeeded)
        {
            return Ok(result.Response);
        }

        return result.Error == "The approval request was not found."
            ? NotFound(new { message = result.Error })
            : Conflict(new { message = result.Error });
    }
}
