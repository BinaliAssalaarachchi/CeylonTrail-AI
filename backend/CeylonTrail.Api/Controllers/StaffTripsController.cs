using CeylonTrail.Api.DTOs.Trips;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CeylonTrail.Api.Controllers;

[ApiController]
[Route("api/staff/trips")]
[Authorize(Roles = nameof(UserRole.TravelCoordinator) + "," + nameof(UserRole.Administrator))]
public sealed class StaffTripsController(ITripService tripService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StaffTripResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await tripService.GetStaffTripsAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StaffTripResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await tripService.GetStaffTripAsync(id, cancellationToken);
        return result.NotFound ? NotFound() : Ok(result.Value);
    }

    [HttpGet("{id:guid}/itinerary")]
    public async Task<ActionResult<ItineraryResponse>> GetItinerary(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await tripService.GetStaffItineraryAsync(id, cancellationToken);
        return result.NotFound ? NotFound() : Ok(result.Value);
    }

    [HttpGet("{id:guid}/itineraries")]
    public async Task<ActionResult<IReadOnlyList<ItineraryHistoryItemResponse>>> GetItineraryHistory(Guid id, CancellationToken cancellationToken)
    {
        var result = await tripService.GetStaffItineraryHistoryAsync(id, cancellationToken);
        return result.NotFound ? NotFound() : Ok(result.Value);
    }

    [HttpGet("{id:guid}/itineraries/{itineraryId:guid}")]
    public async Task<ActionResult<ItineraryResponse>> GetHistoricalItinerary(Guid id, Guid itineraryId, CancellationToken cancellationToken)
    {
        var result = await tripService.GetStaffItineraryAsync(id, itineraryId, cancellationToken);
        return result.NotFound ? NotFound() : Ok(result.Value);
    }
}
