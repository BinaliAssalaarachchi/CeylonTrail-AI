using System.Security.Claims;
using CeylonTrail.Api.DTOs.TravelAlerts;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CeylonTrail.Api.Controllers;

[ApiController]
[Route("api/travel-alerts")]
[Authorize]
public sealed class TravelAlertsController(ITravelAlertService travelAlertService) : ControllerBase
{
    private const string NotFoundError = "Travel alert was not found.";
    private const string ManagementRoles = $"{nameof(UserRole.TravelCoordinator)},{nameof(UserRole.Administrator)}";

    [HttpPost]
    [Authorize(Roles = ManagementRoles)]
    public async Task<ActionResult<TravelAlertResponse>> Create(
        CreateTravelAlertRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(out var authenticatedUserId))
        {
            return Unauthorized(new { message = "The authenticated user ID is missing or invalid." });
        }

        var result = await travelAlertService.CreateAsync(
            request,
            authenticatedUserId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error });
        }

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Response!.Id },
            result.Response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TravelAlertResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await travelAlertService.GetByIdAsync(id, cancellationToken);

        return result.Succeeded
            ? Ok(result.Response)
            : NotFound(new { message = result.Error });
    }

    [HttpGet]
    public async Task<ActionResult<TravelAlertPageResponse>> Query(
        [FromQuery] TravelAlertQueryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await travelAlertService.QueryAsync(request, cancellationToken);

        return result.Succeeded
            ? Ok(result.Response)
            : BadRequest(new { message = result.Error });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<ActionResult<TravelAlertResponse>> Update(
        Guid id,
        UpdateTravelAlertRequest request,
        CancellationToken cancellationToken)
    {
        var result = await travelAlertService.UpdateAsync(id, request, cancellationToken);

        if (result.Succeeded)
        {
            return Ok(result.Response);
        }

        return result.Error == NotFoundError
            ? NotFound(new { message = result.Error })
            : BadRequest(new { message = result.Error });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = ManagementRoles)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await travelAlertService.DeleteAsync(id, cancellationToken);

        return result.Succeeded
            ? NoContent()
            : NotFound(new { message = result.Error });
    }

    private bool TryGetAuthenticatedUserId(out Guid userId)
    {
        var claimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claimValue, out userId);
    }
}
