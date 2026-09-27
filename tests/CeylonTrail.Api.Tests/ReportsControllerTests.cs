using System.Security.Claims;
using CeylonTrail.Api.Controllers;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Reports;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class ReportsControllerTests
{
    [Fact]
    public async Task Overview_ReturnsAggregatedOperationalCountsAndStatusGroups()
    {
        await using var db = CreateDbContext();
        var tourist = AddUser(db, UserRole.Tourist);
        var coordinator = AddUser(db, UserRole.TravelCoordinator);
        var draftTrip = new Trip { Id = Guid.NewGuid(), TouristId = tourist.Id, Name = "Draft", StartDate = DateOnly.FromDateTime(DateTime.UtcNow), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), Budget = 100, Status = TripStatus.Draft };
        var plannedTrip = new Trip { Id = Guid.NewGuid(), TouristId = tourist.Id, Name = "Planned", StartDate = DateOnly.FromDateTime(DateTime.UtcNow), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), Budget = 100, Status = TripStatus.Planned };
        db.Trips.AddRange(draftTrip, plannedTrip);
        db.Bookings.AddRange(
            new Booking { Id = Guid.NewGuid(), UserId = tourist.Id, CurrentStatus = BookingStatus.Draft, TotalAmount = 10 },
            new Booking { Id = Guid.NewGuid(), UserId = tourist.Id, CurrentStatus = BookingStatus.Confirmed, TotalAmount = 20 });
        db.ApprovalRequests.AddRange(
            new ApprovalRequest { Id = Guid.NewGuid(), ValidationResultId = Guid.NewGuid(), RequestedByUserId = tourist.Id, Status = ApprovalRequestStatus.Pending, RecommendedAction = ApprovalRecommendedAction.Proceed, RiskLevel = ValidationRiskLevel.High, Summary = "Pending", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new ApprovalRequest { Id = Guid.NewGuid(), ValidationResultId = Guid.NewGuid(), RequestedByUserId = tourist.Id, Status = ApprovalRequestStatus.Approved, RecommendedAction = ApprovalRecommendedAction.Proceed, RiskLevel = ValidationRiskLevel.Low, Summary = "Approved", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.TravelAlerts.AddRange(
            new TravelAlert { Id = Guid.NewGuid(), Title = "Active high", Description = "Alert", AlertType = TravelAlertType.Safety, Severity = TravelAlertSeverity.High, District = "Kandy", StartDateTime = DateTime.UtcNow, EndDateTime = DateTime.UtcNow.AddDays(1), Status = TravelAlertStatus.Active, CreatedByUserId = coordinator.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new TravelAlert { Id = Guid.NewGuid(), Title = "Draft critical", Description = "Alert", AlertType = TravelAlertType.Safety, Severity = TravelAlertSeverity.Critical, District = "Kandy", StartDateTime = DateTime.UtcNow, EndDateTime = DateTime.UtcNow.AddDays(1), Status = TravelAlertStatus.Draft, CreatedByUserId = coordinator.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.AgentWorkflows.AddRange(
            new AgentWorkflow { Id = Guid.NewGuid(), WorkflowId = Guid.NewGuid(), TripId = draftTrip.Id, RequestedByUserId = tourist.Id, Status = AgentWorkflowStatus.AwaitingApproval, StartedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new AgentWorkflow { Id = Guid.NewGuid(), WorkflowId = Guid.NewGuid(), TripId = plannedTrip.Id, RequestedByUserId = tourist.Id, Status = AgentWorkflowStatus.Completed, StartedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new AgentWorkflow { Id = Guid.NewGuid(), WorkflowId = Guid.NewGuid(), TripId = plannedTrip.Id, RequestedByUserId = tourist.Id, Status = AgentWorkflowStatus.FailedSafe, StartedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var response = await new ReportService(db).GetOverviewAsync();

        Assert.Equal(2, response.TotalTrips);
        Assert.Equal(1, response.TripsByStatus["Draft"]);
        Assert.Equal(1, response.TripsByStatus["Planned"]);
        Assert.Equal(2, response.TotalBookings);
        Assert.Equal(1, response.BookingsByStatus["Draft"]);
        Assert.Equal(1, response.BookingsByStatus["Confirmed"]);
        Assert.Equal(2, response.TotalApprovalRequests);
        Assert.Equal(1, response.PendingApprovalRequests);
        Assert.Equal(1, response.ActiveTravelAlerts);
        Assert.Equal(1, response.ActiveAlertsBySeverity["High"]);
        Assert.Equal(3, response.TotalWorkflows);
        Assert.Equal(1, response.AwaitingApprovalWorkflows);
        Assert.Equal(1, response.CompletedWorkflows);
        Assert.Equal(1, response.FailedSafeWorkflows);
    }

    [Fact]
    public async Task Overview_ReturnsZerosForEmptyDatabase()
    {
        await using var db = CreateDbContext();

        var response = await new ReportService(db).GetOverviewAsync();

        Assert.Equal(0, response.TotalTrips);
        Assert.Empty(response.TripsByStatus);
        Assert.Equal(0, response.TotalBookings);
        Assert.Empty(response.BookingsByStatus);
        Assert.Equal(0, response.PendingApprovalRequests);
        Assert.Equal(0, response.ActiveTravelAlerts);
        Assert.Equal(0, response.TotalWorkflows);
    }

    [Fact]
    public void Controller_IsRestrictedToTravelCoordinatorAndAdministrator()
    {
        var authorization = typeof(ReportsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.Equal("TravelCoordinator,Administrator", authorization.Roles);
    }

    [Theory]
    [InlineData(UserRole.Tourist)]
    [InlineData(UserRole.TourismProvider)]
    public void NonStaffRolesAreNotIncludedInAuthorizationPolicy(UserRole role)
    {
        var authorization = typeof(ReportsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .Single();

        Assert.DoesNotContain(role.ToString(), authorization.Roles, StringComparison.Ordinal);
    }

    private static User AddUser(ApplicationDbContext db, UserRole role)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), FirstName = role.ToString(), LastName = "User",
            Email = $"{Guid.NewGuid():N}@example.com", PasswordHash = "hash", Role = role,
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        return user;
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
