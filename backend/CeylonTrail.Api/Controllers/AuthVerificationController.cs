using System.Security.Claims;
using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CeylonTrail.Api.Controllers;

// Temporary foundation checks for verifying role authorization through Swagger.
[ApiController]
[Route("api/auth-test")]
[Authorize]
public sealed class AuthVerificationController : ControllerBase
{
    [HttpGet("authenticated")]
    public IActionResult Authenticated() => Ok(new { message = "Authenticated request accepted." });

    [HttpGet("tourist")]
    [Authorize(Roles = nameof(UserRole.Tourist))]
    public IActionResult Tourist() => RoleAccepted(UserRole.Tourist);

    [HttpGet("tourism-provider")]
    [Authorize(Roles = nameof(UserRole.TourismProvider))]
    public IActionResult TourismProvider() => RoleAccepted(UserRole.TourismProvider);

    [HttpGet("travel-coordinator")]
    [Authorize(Roles = nameof(UserRole.TravelCoordinator))]
    public IActionResult TravelCoordinator() => RoleAccepted(UserRole.TravelCoordinator);

    [HttpGet("administrator")]
    [Authorize(Roles = nameof(UserRole.Administrator))]
    public IActionResult Administrator() => RoleAccepted(UserRole.Administrator);

    private IActionResult RoleAccepted(UserRole role) => Ok(new
    {
        message = $"{role} role authorized.",
        userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
    });
}
