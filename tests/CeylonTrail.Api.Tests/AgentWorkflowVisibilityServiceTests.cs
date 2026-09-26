using System.Text.Json;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.Controllers;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class AgentWorkflowVisibilityServiceTests
{
    [Fact]
    public async Task TouristCanReadOnlyOwnLatestWorkflow()
    {
        await using var db = CreateDbContext();
        var owner = AddUser(db, UserRole.Tourist);
        var trip = AddTrip(db, owner.Id);
        AddWorkflow(db, owner, trip, AgentWorkflowStatus.Running);
        await db.SaveChangesAsync();
        var service = new AgentWorkflowVisibilityService(db);

        var own = await service.GetLatestForTouristAsync(owner.Id, trip.Id);
        var other = await service.GetLatestForTouristAsync(Guid.NewGuid(), trip.Id);

        Assert.NotNull(own);
        Assert.Equal(trip.Id, own!.TripId);
        Assert.Null(other);
    }

    [Fact]
    public async Task TouristResponseReturnsOrderedSafeStagesWithoutRawSnapshots()
    {
        await using var db = CreateDbContext();
        var owner = AddUser(db, UserRole.Tourist);
        var trip = AddTrip(db, owner.Id);
        var workflow = AddWorkflow(db, owner, trip, AgentWorkflowStatus.AwaitingApproval);
        workflow.Stages = Enumerable.Range(1, 4).Select(sequence => new AgentWorkflowStage
        {
            Id = Guid.NewGuid(), AgentWorkflowId = workflow.Id, AgentRole = (AgentWorkflowAgentRole)(sequence - 1),
            Sequence = sequence, Status = AgentWorkflowStageStatus.Completed,
            OutputSnapshotJson = JsonSerializer.Serialize(new { secret = "hidden", dayCount = sequence, requiresApproval = sequence == 4 })
        }).ToList();
        await db.SaveChangesAsync();

        var response = await new AgentWorkflowVisibilityService(db).GetLatestForTouristAsync(owner.Id, trip.Id);

        Assert.Equal(new[] { AgentWorkflowAgentRole.Planner, AgentWorkflowAgentRole.Destination, AgentWorkflowAgentRole.BookingAction, AgentWorkflowAgentRole.TravelIntelligence }, response!.Stages.Select(stage => stage.AgentRole));
        Assert.DoesNotContain("hidden", JsonSerializer.Serialize(response));
    }

    [Fact]
    public async Task StaffResponseIncludesExecutionAndBookingProposalButNotRawSnapshot()
    {
        await using var db = CreateDbContext();
        var owner = AddUser(db, UserRole.Tourist);
        var trip = AddTrip(db, owner.Id);
        var workflow = AddWorkflow(db, owner, trip, AgentWorkflowStatus.Completed);
        var approval = new ApprovalRequest
        {
            Id = Guid.NewGuid(), ValidationResultId = Guid.NewGuid(), RequestedByUserId = owner.Id,
            Status = ApprovalRequestStatus.Approved, RecommendedAction = ApprovalRecommendedAction.Proceed,
            RiskLevel = ValidationRiskLevel.High, Summary = "Review", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            Decision = new ApprovalDecision { Id = Guid.NewGuid(), ApprovalRequestId = Guid.Empty, DecidedByUserId = Guid.NewGuid(), Decision = ApprovalDecisionType.Approved, DecidedAt = DateTime.UtcNow }
        };
        db.ValidationResults.Add(new ValidationResult { Id = approval.ValidationResultId, CreatedByUserId = owner.Id, OverallStatus = ValidationOverallStatus.Valid, RiskLevel = ValidationRiskLevel.High, IsFeasible = true, CreatedAt = DateTime.UtcNow });
        var bookingStage = new AgentWorkflowStage
        {
            Id = Guid.NewGuid(), AgentWorkflowId = workflow.Id, AgentRole = AgentWorkflowAgentRole.BookingAction,
            Sequence = 3, Status = AgentWorkflowStageStatus.Completed,
            OutputSnapshotJson = JsonSerializer.Serialize(new
            {
                contract = "CeylonTrail.BookingActionProposal.v1", workflowId = workflow.WorkflowId, tripId = trip.Id,
                status = "Prepared", requiresApproval = true, summary = "Review",
                proposals = new[] { new { attractionId = Guid.NewGuid(), availabilitySlotId = Guid.NewGuid(), guestCount = 1, proposedUnitPrice = 50m, proposedTotalPrice = 50m, startTime = DateTime.UtcNow.AddDays(1), endTime = DateTime.UtcNow.AddDays(1).AddHours(2), reason = "safe" } },
                execution = new { succeeded = true, bookingId = Guid.NewGuid(), error = (string?)null }, secret = "hidden"
            })
        };
        var intelligenceStage = new AgentWorkflowStage
        {
            Id = Guid.NewGuid(), AgentWorkflowId = workflow.Id, AgentRole = AgentWorkflowAgentRole.TravelIntelligence,
            Sequence = 4, Status = AgentWorkflowStageStatus.Completed, ApprovalRequestId = approval.Id
        };
        approval.Decision!.ApprovalRequestId = approval.Id;
        db.ApprovalRequests.Add(approval);
        db.AgentWorkflowStages.AddRange(bookingStage, intelligenceStage);
        await db.SaveChangesAsync();

        var visibility = new AgentWorkflowVisibilityService(db);
        var response = await visibility.GetForStaffAsync(workflow.WorkflowId);
        var persistedApprovalId = db.ApprovalRequests.Single().Id;
        var approvalResponse = await new ApprovalRequestService(db, workflowVisibilityService: visibility).GetByIdAsync(persistedApprovalId);

        Assert.True(response!.ExecutionSucceeded);
        Assert.Single(response.BookingProposals);
        Assert.DoesNotContain("hidden", JsonSerializer.Serialize(response));
        Assert.NotNull(approvalResponse);
        Assert.Equal(true, approvalResponse!.ExecutionSucceeded);
        Assert.NotNull(approvalResponse.BookingId);
    }

    [Fact]
    public void WorkflowControllerIsStaffReadOnly()
    {
        var authorize = typeof(AgentWorkflowsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        Assert.Equal("TravelCoordinator,Administrator", authorize.Roles);
        Assert.DoesNotContain(typeof(AgentWorkflowsController).GetMethods(), method => method.GetCustomAttributes(inherit: true).Any(attribute => attribute is HttpPostAttribute or HttpPutAttribute or HttpDeleteAttribute or HttpPatchAttribute));
    }

    private static AgentWorkflow AddWorkflow(ApplicationDbContext db, User owner, Trip trip, AgentWorkflowStatus status) {
        var workflow = new AgentWorkflow { Id = Guid.NewGuid(), WorkflowId = Guid.NewGuid(), TripId = trip.Id, Trip = trip, RequestedByUserId = owner.Id, RequestedByUser = owner, Status = status, StartedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.AgentWorkflows.Add(workflow);
        return workflow;
    }

    private static Trip AddTrip(ApplicationDbContext db, Guid ownerId) {
        var trip = new Trip { Id = Guid.NewGuid(), TouristId = ownerId, Name = "Trip", StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)), Budget = 1000m, Status = TripStatus.Planned, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Trips.Add(trip);
        return trip;
    }

    private static User AddUser(ApplicationDbContext db, UserRole role) {
        var user = new User { Id = Guid.NewGuid(), FirstName = "Test", LastName = "User", Email = $"{Guid.NewGuid()}@example.com", PasswordHash = "hash", Role = role, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        return user;
    }

    private static ApplicationDbContext CreateDbContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
