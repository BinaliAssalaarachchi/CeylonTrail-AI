using CeylonTrail.Api.DTOs.TravelIntelligence;
using CeylonTrail.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CeylonTrail.Api.Controllers;

[ApiController]
[Route("api/travel-intelligence/executions")]
[Authorize(Roles = "TravelCoordinator,Administrator")]
public sealed class TravelIntelligenceExecutionsController(ITravelIntelligenceExecutionQueryService queryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TravelIntelligenceExecutionPageResponse>> List(
        [FromQuery] TravelIntelligenceExecutionQuery query,
        CancellationToken cancellationToken) =>
        Ok(await queryService.ListForStaffAsync(query, cancellationToken));

    [HttpGet("{executionId:guid}")]
    public async Task<ActionResult<TravelIntelligenceExecutionDetailResponse>> GetById(
        Guid executionId,
        CancellationToken cancellationToken)
    {
        var result = await queryService.GetForStaffAsync(executionId, cancellationToken);
        return result is null ? NotFound(new { message = "Travel Intelligence execution was not found." }) : Ok(result);
    }
}
