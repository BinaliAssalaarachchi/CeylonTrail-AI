using CeylonTrail.Api.Data;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class AgentWorkflowPersistenceServiceTests
{
    [Fact]
    public async Task Create_PersistsPendingWorkflowForTripOwner()
    {
        await using var db = CreateDbContext();
        var (user, trip) = await SeedTripAsync(db);
        var workflowId = Guid.NewGuid();

        var result = await Service(db).CreateAsync(workflowId, trip.Id, user.Id);

        Assert.True(result.Succeeded);
        Assert.Equal(AgentWorkflowStatus.Pending, result.Workflow!.Status);
        Assert.Equal(trip.Id, result.Workflow.TripId);
        Assert.Equal(user.Id, result.Workflow.RequestedByUserId);
        Assert.Equal(workflowId, (await db.AgentWorkflows.SingleAsync()).WorkflowId);
    }

    [Fact]
    public async Task Create_IsIdempotentForSameWorkflowTripAndOwner()
    {
        await using var db = CreateDbContext();
        var (user, trip) = await SeedTripAsync(db);
        var workflowId = Guid.NewGuid();
        var service = Service(db);

        var first = await service.CreateAsync(workflowId, trip.Id, user.Id);
        var second = await service.CreateAsync(workflowId, trip.Id, user.Id);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Workflow!.Id, second.Workflow!.Id);
        Assert.Equal(1, await db.AgentWorkflows.CountAsync());
    }

    [Fact]
    public async Task Create_RejectsWorkflowIdReuseForAnotherTripOrOwner()
    {
        await using var db = CreateDbContext();
        var (user, trip) = await SeedTripAsync(db);
        var otherUser = new User { Id = Guid.NewGuid(), FirstName = "Other", LastName = "Tourist", Email = "other@example.com", PasswordHash = "hash", Role = UserRole.Tourist, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Users.Add(otherUser);
        var otherTrip = new Trip { Id = Guid.NewGuid(), TouristId = otherUser.Id, Name = "Other trip", StartDate = trip.StartDate, EndDate = trip.EndDate, Status = TripStatus.Draft, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Trips.Add(otherTrip);
        await db.SaveChangesAsync();
        var service = Service(db);
        var workflowId = Guid.NewGuid();

        Assert.True((await service.CreateAsync(workflowId, trip.Id, user.Id)).Succeeded);
        var result = await service.CreateAsync(workflowId, otherTrip.Id, otherUser.Id);

        Assert.False(result.Succeeded);
        Assert.Contains("already associated", result.Error);
    }

    [Fact]
    public async Task Create_RejectsRequesterWhoDoesNotOwnTrip()
    {
        await using var db = CreateDbContext();
        var (user, trip) = await SeedTripAsync(db);
        var otherUser = new User { Id = Guid.NewGuid(), FirstName = "Other", LastName = "Tourist", Email = "other2@example.com", PasswordHash = "hash", Role = UserRole.Tourist, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Users.Add(otherUser);
        await db.SaveChangesAsync();

        var result = await Service(db).CreateAsync(Guid.NewGuid(), trip.Id, otherUser.Id);

        Assert.False(result.Succeeded);
        Assert.Contains("does not own", result.Error);
    }

    [Fact]
    public async Task StartStage_PersistsRunningStageAndInputSnapshot()
    {
        await using var db = CreateDbContext();
        var (user, trip) = await SeedTripAsync(db);
        var service = Service(db);
        var workflowId = Guid.NewGuid();
        await service.CreateAsync(workflowId, trip.Id, user.Id);

        var result = await service.StartStageAsync(workflowId, AgentWorkflowAgentRole.Planner, 1, inputSnapshotJson: "{\"objective\":\"beaches\"}");

        Assert.True(result.Succeeded);
        Assert.Equal(AgentWorkflowStatus.Running, result.Workflow!.Status);
        Assert.Equal(AgentWorkflowStageStatus.Running, result.Stage!.Status);
        Assert.Equal("{\"objective\":\"beaches\"}", result.Stage.InputSnapshotJson);
    }

    [Fact]
    public async Task StartStage_RejectsInvalidJsonAndNonPositiveSequence()
    {
        await using var db = CreateDbContext();
        var (user, trip) = await SeedTripAsync(db);
        var service = Service(db);
        var workflowId = Guid.NewGuid();
        await service.CreateAsync(workflowId, trip.Id, user.Id);

        var invalidJson = await service.StartStageAsync(workflowId, AgentWorkflowAgentRole.Planner, 1, inputSnapshotJson: "not-json");
        var invalidSequence = await service.StartStageAsync(workflowId, AgentWorkflowAgentRole.Planner, 0);

        Assert.False(invalidJson.Succeeded);
        Assert.False(invalidSequence.Succeeded);
        Assert.Empty(db.AgentWorkflowStages);
    }

    [Fact]
    public async Task StartStage_RejectsDuplicateAgentAttemptButAllowsRetryAttempt()
    {
        await using var db = CreateDbContext();
        var (user, trip) = await SeedTripAsync(db);
        var service = Service(db);
        var workflowId = Guid.NewGuid();
        await service.CreateAsync(workflowId, trip.Id, user.Id);

        var first = await service.StartStageAsync(workflowId, AgentWorkflowAgentRole.Destination, 2);
        var duplicate = await service.StartStageAsync(workflowId, AgentWorkflowAgentRole.Destination, 2);
        var retry = await service.StartStageAsync(workflowId, AgentWorkflowAgentRole.Destination, 2, 2);

        Assert.True(first.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.True(retry.Succeeded);
        Assert.Equal(1, retry.Workflow!.RetryCount);
        Assert.Equal(2, await db.AgentWorkflowStages.CountAsync());
    }

    [Fact]
    public async Task CompleteStage_PersistsOutputAndFutureEntityReferences()
    {
        await using var db = CreateDbContext();
        var (user, trip) = await SeedTripAsync(db);
        var service = Service(db);
        var workflowId = Guid.NewGuid();
        await service.CreateAsync(workflowId, trip.Id, user.Id);
        var started = await service.StartStageAsync(workflowId, AgentWorkflowAgentRole.TravelIntelligence, 4);

        var validationId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        var result = await service.CompleteStageAsync(workflowId, started.Stage!.Id, "{\"risk\":\"low\"}", validationId, executionId, approvalId);

        Assert.True(result.Succeeded);
        Assert.Equal(AgentWorkflowStageStatus.Completed, result.Stage!.Status);
        Assert.Equal(validationId, result.Stage.ValidationResultId);
        Assert.Equal(executionId, result.Stage.TravelIntelligenceExecutionId);
        Assert.Equal(approvalId, result.Stage.ApprovalRequestId);
    }

    [Fact]
    public async Task CompleteStage_RejectsSecondCompletion()
    {
        await using var db = CreateDbContext();
        var (user, trip) = await SeedTripAsync(db);
        var service = Service(db);
        var workflowId = Guid.NewGuid();
        await service.CreateAsync(workflowId, trip.Id, user.Id);
        var started = await service.StartStageAsync(workflowId, AgentWorkflowAgentRole.BookingAction, 3);
        Assert.True((await service.CompleteStageAsync(workflowId, started.Stage!.Id)).Succeeded);

        var second = await service.CompleteStageAsync(workflowId, started.Stage.Id);

        Assert.False(second.Succeeded);
    }

    [Fact]
    public async Task FailStage_MarksWorkflowFailedSafeAndBoundsFailureFields()
    {
        await using var db = CreateDbContext();
        var (user, trip) = await SeedTripAsync(db);
        var service = Service(db);
        var workflowId = Guid.NewGuid();
        await service.CreateAsync(workflowId, trip.Id, user.Id);
        var started = await service.StartStageAsync(workflowId, AgentWorkflowAgentRole.Planner, 1);

        var result = await service.FailStageAsync(workflowId, started.Stage!.Id, new string('c', 200), new string('s', 2000));

        Assert.True(result.Succeeded);
        Assert.Equal(AgentWorkflowStatus.FailedSafe, result.Workflow!.Status);
        Assert.Equal(100, result.Workflow.FailureCode!.Length);
        Assert.Equal(1000, result.Workflow.FailureSummary!.Length);
        Assert.Equal(AgentWorkflowStageStatus.Failed, result.Stage!.Status);
    }

    [Fact]
    public async Task Transition_RequiresFailureDetailsForFailedSafe()
    {
        await using var db = CreateDbContext();
        var (user, trip) = await SeedTripAsync(db);
        var service = Service(db);
        var workflowId = Guid.NewGuid();
        await service.CreateAsync(workflowId, trip.Id, user.Id);

        var result = await service.TransitionAsync(workflowId, AgentWorkflowStatus.FailedSafe);

        Assert.False(result.Succeeded);
        Assert.Equal(AgentWorkflowStatus.Pending, (await service.GetByWorkflowIdAsync(workflowId)).Workflow!.Status);
    }

    [Fact]
    public async Task Transition_RejectsTerminalWorkflowRestart()
    {
        await using var db = CreateDbContext();
        var (user, trip) = await SeedTripAsync(db);
        var service = Service(db);
        var workflowId = Guid.NewGuid();
        await service.CreateAsync(workflowId, trip.Id, user.Id);
        Assert.True((await service.TransitionAsync(workflowId, AgentWorkflowStatus.Running)).Succeeded);
        Assert.True((await service.TransitionAsync(workflowId, AgentWorkflowStatus.Completed)).Succeeded);

        var result = await service.TransitionAsync(workflowId, AgentWorkflowStatus.Running);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Transition_AllowsAwaitingApprovalThenCompleted()
    {
        await using var db = CreateDbContext();
        var (user, trip) = await SeedTripAsync(db);
        var service = Service(db);
        var workflowId = Guid.NewGuid();
        await service.CreateAsync(workflowId, trip.Id, user.Id);
        await service.TransitionAsync(workflowId, AgentWorkflowStatus.Running);

        var awaiting = await service.TransitionAsync(workflowId, AgentWorkflowStatus.AwaitingApproval, AgentWorkflowAgentRole.TravelIntelligence);
        var completed = await service.TransitionAsync(workflowId, AgentWorkflowStatus.Completed);

        Assert.True(awaiting.Succeeded);
        Assert.True(completed.Succeeded);
        Assert.NotNull(completed.Workflow!.CompletedAt);
    }

    [Fact]
    public async Task GetByWorkflowId_CanBeOwnerScoped()
    {
        await using var db = CreateDbContext();
        var (user, trip) = await SeedTripAsync(db);
        var service = Service(db);
        var workflowId = Guid.NewGuid();
        await service.CreateAsync(workflowId, trip.Id, user.Id);

        var wrongOwner = await service.GetByWorkflowIdAsync(workflowId, Guid.NewGuid());
        var owner = await service.GetByWorkflowIdAsync(workflowId, user.Id);

        Assert.False(wrongOwner.Succeeded);
        Assert.True(owner.Succeeded);
    }

    [Fact]
    public void WorkflowVisibilityControllerExposesNoMutationActions()
    {
        var workflowControllers = typeof(AgentWorkflowPersistenceService).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && type.Name.Contains("Workflow", StringComparison.OrdinalIgnoreCase));

        Assert.All(workflowControllers, controller =>
        {
            var methods = controller.GetMethods();
            Assert.DoesNotContain(methods, method => method.GetCustomAttributes(inherit: true).Any(attribute =>
                attribute is HttpPostAttribute or HttpPutAttribute or HttpDeleteAttribute or HttpPatchAttribute));
        });
    }

    private static AgentWorkflowPersistenceService Service(ApplicationDbContext db) => new(db);

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(User User, Trip Trip)> SeedTripAsync(ApplicationDbContext db)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), FirstName = "Test", LastName = "Tourist", Email = $"{Guid.NewGuid()}@example.com",
            PasswordHash = "hash", Role = UserRole.Tourist, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var trip = new Trip
        {
            Id = Guid.NewGuid(), TouristId = user.Id, Name = "Test trip", StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 5), Budget = 100000m, Status = TripStatus.Draft,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        db.Trips.Add(trip);
        await db.SaveChangesAsync();
        return (user, trip);
    }
}
