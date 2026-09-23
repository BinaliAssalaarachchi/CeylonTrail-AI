using System.Security.Claims;
using CeylonTrail.Api.Controllers;
using CeylonTrail.Api.DTOs.TravelAlerts;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class TravelAlertsControllerTests
{
    [Fact]
    public async Task Create_UsesAuthenticatedNameIdentifierClaim()
    {
        var authenticatedUserId = Guid.NewGuid();
        var service = new RecordingTravelAlertService();
        var controller = CreateController(service, authenticatedUserId);

        var result = await controller.Create(new CreateTravelAlertRequest(), CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(authenticatedUserId, service.AuthenticatedUserId);
        Assert.Equal(nameof(TravelAlertsController.GetById), createdResult.ActionName);
    }

    [Fact]
    public async Task Create_WithoutValidNameIdentifierClaim_ReturnsUnauthorized()
    {
        var service = new RecordingTravelAlertService();
        var controller = CreateController(service, userIdClaim: "not-a-guid");

        var result = await controller.Create(new CreateTravelAlertRequest(), CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Null(service.AuthenticatedUserId);
    }

    [Fact]
    public void Controller_UsesExpectedAuthenticationAndManagementRoles()
    {
        var controllerAuthorization = typeof(TravelAlertsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Null(controllerAuthorization.Roles);
        Assert.Equal(
            "TravelCoordinator,Administrator",
            GetMethodAuthorization(nameof(TravelAlertsController.Create))!.Roles);
        Assert.Equal(
            "TravelCoordinator,Administrator",
            GetMethodAuthorization(nameof(TravelAlertsController.Update))!.Roles);
        Assert.Equal(
            "TravelCoordinator,Administrator",
            GetMethodAuthorization(nameof(TravelAlertsController.Delete))!.Roles);
        Assert.Null(GetMethodAuthorization(nameof(TravelAlertsController.GetById)));
    }

    private static TravelAlertsController CreateController(
        ITravelAlertService service,
        Guid? authenticatedUserId = null,
        string? userIdClaim = null)
    {
        var claimValue = userIdClaim ?? authenticatedUserId?.ToString();
        var claims = claimValue is null
            ? Array.Empty<Claim>()
            : new[] { new Claim(ClaimTypes.NameIdentifier, claimValue) };

        var controller = new TravelAlertsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
                }
            }
        };

        return controller;
    }

    private static AuthorizeAttribute? GetMethodAuthorization(string methodName) =>
        typeof(TravelAlertsController)
            .GetMethod(methodName)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();

    private sealed class RecordingTravelAlertService : ITravelAlertService
    {
        public Guid? AuthenticatedUserId { get; private set; }

        public Task<(bool Succeeded, string? Error, TravelAlertResponse? Response)> CreateAsync(
            CreateTravelAlertRequest request,
            Guid authenticatedUserId,
            CancellationToken cancellationToken = default)
        {
            AuthenticatedUserId = authenticatedUserId;
            return Task.FromResult<(bool, string?, TravelAlertResponse?)>(
                (true, null, new TravelAlertResponse { Id = Guid.NewGuid() }));
        }

        public Task<(bool Succeeded, string? Error, TravelAlertResponse? Response)> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(bool, string?, TravelAlertResponse?)>((false, "Travel alert was not found.", null));

        public Task<(bool Succeeded, string? Error, TravelAlertPageResponse? Response)> QueryAsync(
            TravelAlertQueryRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(bool, string?, TravelAlertPageResponse?)>((true, null, new TravelAlertPageResponse()));

        public Task<(bool Succeeded, string? Error, TravelAlertResponse? Response)> UpdateAsync(
            Guid id,
            UpdateTravelAlertRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(bool, string?, TravelAlertResponse?)>((false, "Travel alert was not found.", null));

        public Task<(bool Succeeded, string? Error)> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(bool, string?)>((false, "Travel alert was not found."));
    }
}
