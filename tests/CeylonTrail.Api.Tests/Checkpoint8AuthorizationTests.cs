using System.Reflection;
using CeylonTrail.Api.Controllers;
using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class Checkpoint8AuthorizationTests
{
    [Fact]
    public void BookingCreationIsTouristOnlyWhileOperationalActionsAreStaffOnly()
    {
        var controllerType = typeof(BookingsController);

        Assert.Equal(
            nameof(UserRole.Tourist),
            AttributeFor(controllerType, "Create").Roles);

        var operationalRoles = $"{nameof(UserRole.TourismProvider)},{nameof(UserRole.TravelCoordinator)},{nameof(UserRole.Administrator)}";
        Assert.Equal(operationalRoles, AttributeFor(controllerType, "Accept").Roles);
        Assert.Equal(operationalRoles, AttributeFor(controllerType, "Reject").Roles);
        Assert.Equal(operationalRoles, AttributeFor(controllerType, "CreateAvailabilitySlot").Roles);
    }

    private static AuthorizeAttribute AttributeFor(Type controllerType, string methodName) =>
        controllerType.GetMethod(methodName)!.GetCustomAttribute<AuthorizeAttribute>()!;
}
