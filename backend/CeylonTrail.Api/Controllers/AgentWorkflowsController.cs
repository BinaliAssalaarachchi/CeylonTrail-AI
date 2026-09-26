using CeylonTrail.Api.DTOs.AgentWorkflows;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CeylonTrail.Api.Controllers;

[ApiController]
[Route("api/agent-workflows")]
[Authorize(Roles = "TravelCoordinator,Administrator")]
public sealed class AgentWorkflowsController(IAgentWorkflowVisibilityService visibilityService) : ControllerBase
{
    [HttpGet("{workflowId:guid}")]
    public async Task<ActionResult<StaffAgentWorkflowResponse>> Get(
        Guid workflowId,
        CancellationToken cancellationToken)
    {
        var result = await visibilityService.GetForStaffAsync(workflowId, cancellationToken);
        return result is null
            ? NotFound(new { message = "The agent workflow was not found." })
            : Ok(result);
    }
}
