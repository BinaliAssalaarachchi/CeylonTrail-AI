using System.Security.Claims;
using CeylonTrail.Api.DTOs.Bookings;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CeylonTrail.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public sealed class BookingsController(IBookingService bookingService) : ControllerBase
{
    // 1. POST /api/bookings (Create a booking request - Tourist only)
    [HttpPost]
    [Authorize(Roles = nameof(UserRole.Tourist))]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status201Created)] 
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BookingResponse>> Create(
        [FromBody] CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await bookingService.CreateBookingAsync(userId, request, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Response!.Id }, result.Response);
    }

    // 2. GET /api/bookings (Get current tourist's bookings)
    [HttpGet]
    [Authorize(Roles = nameof(UserRole.Tourist))]
    [ProducesResponseType(typeof(List<BookingResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BookingResponse>>> GetMyBookings(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var bookings = await bookingService.GetTouristBookingsAsync(userId, cancellationToken);
        return Ok(bookings);
    }

    // 3. GET /api/provider/bookings (List incoming bookings for Providers/Staff)
    [HttpGet("/api/provider/bookings")]
    [Authorize(Roles = $"{nameof(UserRole.TourismProvider)},{nameof(UserRole.TravelCoordinator)},{nameof(UserRole.Administrator)}")]
    [ProducesResponseType(typeof(List<BookingResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<BookingResponse>>> GetProviderBookings(CancellationToken cancellationToken)
    {
        var bookings = await bookingService.GetProviderBookingsAsync(cancellationToken);
        return Ok(bookings);
    }

    // 4. GET /api/bookings/{id} (Get booking details)
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BookingResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();
        var result = await bookingService.GetBookingByIdAsync(id, userId, role, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Error });
        }

        return Ok(result.Response);
    }

    // 5. POST /api/bookings/{id}/accept (Accept a booking - Provider/Staff only)
    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = $"{nameof(UserRole.TourismProvider)},{nameof(UserRole.TravelCoordinator)},{nameof(UserRole.Administrator)}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BookingResponse>> Accept(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await bookingService.AcceptBookingAsync(id, userId, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error });
        }

        return Ok(result.Response);
    }

    // 6. POST /api/bookings/{id}/reject (Reject a booking - Provider/Staff only)
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = $"{nameof(UserRole.TourismProvider)},{nameof(UserRole.TravelCoordinator)},{nameof(UserRole.Administrator)}")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BookingResponse>> Reject(
        Guid id,
        [FromBody] RejectBookingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await bookingService.RejectBookingAsync(id, userId, request, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error });
        }

        return Ok(result.Response);
    }

    // 7. POST /api/bookings/{id}/cancel (Cancel a booking - Tourist or Admin)
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(BookingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BookingResponse>> Cancel(
        Guid id,
        [FromBody] CancelBookingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();
        var result = await bookingService.CancelBookingAsync(id, userId, role, request, cancellationToken);

        if (!result.Succeeded)
        {
            return BadRequest(new { message = result.Error });
        }

        return Ok(result.Response);
    }

    // 8. GET /api/bookings/{id}/history (Status history audit log)
    [HttpGet("{id:guid}/history")]
    [ProducesResponseType(typeof(List<BookingHistoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<BookingHistoryResponse>>> GetHistory(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();
        var result = await bookingService.GetBookingHistoryAsync(id, userId, role, cancellationToken);

        if (!result.Succeeded)
        {
            return NotFound(new { message = result.Error });
        }

        return Ok(result.Response);
    }

    private Guid GetCurrentUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
    }
}
