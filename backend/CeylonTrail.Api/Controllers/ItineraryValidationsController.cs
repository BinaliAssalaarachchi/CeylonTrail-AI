using System.Security.Claims;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.DTOs.TravelIntelligence;
using CeylonTrail.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CeylonTrail.Api.Controllers;

[ApiController]
[Route("api/itinerary-validations")]
[Authorize]
public sealed class ItineraryValidationsController(
    IItineraryValidationService validationService,
    ITravelIntelligenceService travelIntelligenceService,
    ITravelIntelligenceExecutionPersistenceService executionPersistenceService,
    ITravelIntelligenceExecutionQueryService executionQueryService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ItineraryValidationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ItineraryValidationResponse>> Validate(
        [FromBody] ItineraryValidationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
        {
            return Unauthorized(new { message = "The authenticated user ID is missing or invalid." });
        }

        var result = await validationService.ValidateAsync(request, userId, cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Response!.Id }, result.Response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ItineraryValidationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ItineraryValidationResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
        {
            return Unauthorized(new { message = "The authenticated user ID is missing or invalid." });
        }

        var result = await validationService.GetByIdAsync(id, userId, cancellationToken);
        return result is null
            ? NotFound(new { message = "Itinerary validation was not found." })
            : Ok(result);
    }

    [HttpPost("{id:guid}/travel-intelligence")]
    [ProducesResponseType(typeof(TravelIntelligenceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TravelIntelligenceResponse>> AnalyzeWithTravelIntelligence(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var userId))
        {
            return Unauthorized(new { message = "The authenticated user ID is missing or invalid." });
        }

        var validation = await validationService.GetByIdAsync(id, userId, cancellationToken);
        if (validation is null)
        {
            return NotFound(new { message = "Itinerary validation was not found." });
        }

        var recommendation = await travelIntelligenceService.AnalyzeAsync(validation, cancellationToken);
        var persistence = await executionPersistenceService.PersistAsync(
            validation,
            userId,
            recommendation,
            cancellationToken);
        if (!persistence.Succeeded)
        {
            return Problem(
                detail: persistence.Error,
                statusCode: StatusCodes.Status500InternalServerError);
        }

        recommendation.ApprovalRequest = persistence.ApprovalRequest;

        return Ok(recommendation);
    }

    [HttpGet("{id:guid}/travel-intelligence/executions")]
    public async Task<ActionResult<TravelIntelligenceExecutionPageResponse>> ListTravelIntelligenceExecutions(
        Guid id,
        [FromQuery] TravelIntelligenceExecutionQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var userId)) return Unauthorized();
        var result = await executionQueryService.ListForOwnerAsync(id, userId, query, cancellationToken);
        return result is null ? NotFound(new { message = "Itinerary validation was not found." }) : Ok(result);
    }

    [HttpGet("{id:guid}/travel-intelligence/executions/{executionId:guid}")]
    public async Task<ActionResult<TravelIntelligenceExecutionDetailResponse>> GetTravelIntelligenceExecution(
        Guid id,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var userId)) return Unauthorized();
        var result = await executionQueryService.GetForOwnerAsync(id, executionId, userId, cancellationToken);
        return result is null ? NotFound(new { message = "Travel Intelligence execution was not found." }) : Ok(result);
    }

    private bool TryGetAuthenticatedUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
