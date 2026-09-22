using System.Security.Claims;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CeylonTrail.Api.Controllers;

[ApiController]
[Route("api/itinerary-validations")]
[Authorize]
public sealed class ItineraryValidationsController(IItineraryValidationService validationService) : ControllerBase
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

    private bool TryGetAuthenticatedUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
