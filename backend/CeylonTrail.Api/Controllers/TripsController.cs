using System.Security.Claims;
using CeylonTrail.Api.DTOs.Trips;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CeylonTrail.Api.Controllers;

[ApiController]
[Route("api/trips")]
[Authorize(Roles = nameof(UserRole.Tourist))]
public sealed class TripsController(ITripService tripService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TripResponse>> Create(
        CreateTripRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized();
        }

        var result = await tripService.CreateTripAsync(touristId, request, cancellationToken);
        if (result.Error is not null)
        {
            return BadRequest(new { message = result.Error });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TripResponse>>> GetAll(CancellationToken cancellationToken)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized();
        }

        return Ok(await tripService.GetTripsAsync(touristId, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TripResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized();
        }

        var result = await tripService.GetTripAsync(touristId, id, cancellationToken);
        return result.NotFound ? NotFound() : Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TripResponse>> Update(
        Guid id,
        UpdateTripRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized();
        }

        var result = await tripService.UpdateTripAsync(touristId, id, request, cancellationToken);
        if (result.NotFound)
        {
            return NotFound();
        }

        return result.Error is not null
            ? BadRequest(new { message = result.Error })
            : Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized();
        }

        return await tripService.DeleteTripAsync(touristId, id, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpPost("{id:guid}/preferences")]
    public async Task<ActionResult<TripPreferenceResponse>> AddPreference(
        Guid id,
        AddTripPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized();
        }

        var result = await tripService.AddPreferenceAsync(touristId, id, request, cancellationToken);
        if (result.NotFound)
        {
            return NotFound();
        }

        return result.Error is not null
            ? BadRequest(new { message = result.Error })
            : CreatedAtAction(nameof(GetById), new { id }, result.Value);
    }

    [HttpGet("{id:guid}/itinerary")]
    public async Task<ActionResult<ItineraryResponse>> GetItinerary(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized();
        }

        var result = await tripService.GetLatestItineraryAsync(touristId, id, cancellationToken);
        return result.NotFound ? NotFound() : Ok(result.Value);
    }

    [HttpPost("{id:guid}/generate-itinerary")]
    public async Task<ActionResult<ItineraryResponse>> GenerateItinerary(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!TryGetTouristId(out var touristId))
        {
            return Unauthorized();
        }

        var result = await tripService.GenerateItineraryAsync(touristId, id, cancellationToken);
        if (result.NotFound)
        {
            return NotFound();
        }

        return result.ServiceUnavailable
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Planner service is temporarily unavailable. Please try again." })
            : result.Error is not null
            ? BadRequest(new { message = result.Error })
            : Ok(result.Value);
    }

    [HttpGet("{id:guid}/itineraries")]
    public async Task<ActionResult<IReadOnlyList<ItineraryHistoryItemResponse>>> GetItineraryHistory(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetTouristId(out var touristId)) return Unauthorized();
        var result = await tripService.GetItineraryHistoryAsync(touristId, id, cancellationToken);
        return result.NotFound ? NotFound() : Ok(result.Value);
    }

    [HttpGet("{id:guid}/itineraries/{itineraryId:guid}")]
    public async Task<ActionResult<ItineraryResponse>> GetHistoricalItinerary(Guid id, Guid itineraryId, CancellationToken cancellationToken)
    {
        if (!TryGetTouristId(out var touristId)) return Unauthorized();
        var result = await tripService.GetItineraryAsync(touristId, id, itineraryId, cancellationToken);
        return result.NotFound ? NotFound() : Ok(result.Value);
    }

    private bool TryGetTouristId(out Guid touristId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out touristId) &&
        touristId != Guid.Empty;
}
