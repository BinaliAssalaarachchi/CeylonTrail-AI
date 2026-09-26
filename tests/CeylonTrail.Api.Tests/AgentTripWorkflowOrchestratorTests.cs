using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.ApprovalRequests;
using CeylonTrail.Api.DTOs.Attractions;
using CeylonTrail.Api.DTOs.BookingAction;
using CeylonTrail.Api.DTOs.Planner;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class AgentTripWorkflowOrchestratorTests
{
    [Fact]
    public async Task SuccessfulRunPersistsFourStagesInOrderAndDoesNotCreateBooking()
    {
        await using var db = CreateDbContext();
        var scenario = await CreateScenarioAsync(db);
        var intelligence = new FakeIntelligence();
        var orchestrator = CreateOrchestrator(db, scenario, intelligence);

        var result = await orchestrator.ExecuteAsync(scenario.Tourist.Id, scenario.Trip.Id);

        Assert.NotNull(result.Value);
        var workflow = await db.AgentWorkflows.Include(item => item.Stages).SingleAsync();
        Assert.Equal(AgentWorkflowStatus.Completed, workflow.Status);
        Assert.Equal(
            new[] { AgentWorkflowAgentRole.Planner, AgentWorkflowAgentRole.Destination, AgentWorkflowAgentRole.BookingAction, AgentWorkflowAgentRole.TravelIntelligence },
            workflow.Stages.OrderBy(stage => stage.Sequence).Select(stage => stage.AgentRole));
        Assert.All(workflow.Stages, stage => Assert.Equal(AgentWorkflowStageStatus.Completed, stage.Status));
        Assert.Empty(db.Bookings);
        Assert.Empty(db.BookingItems);
        Assert.Equal(1, intelligence.CallCount);
    }

    [Fact]
    public async Task ApprovalRequiredOutcomeLeavesWorkflowAwaitingApproval()
    {
        await using var db = CreateDbContext();
        var scenario = await CreateScenarioAsync(db);
        var intelligence = new FakeIntelligence { ApprovalRequired = true };
        var orchestrator = CreateOrchestrator(db, scenario, intelligence);

        await orchestrator.ExecuteAsync(scenario.Tourist.Id, scenario.Trip.Id);

        var workflow = await db.AgentWorkflows.SingleAsync();
        Assert.Equal(AgentWorkflowStatus.AwaitingApproval, workflow.Status);
    }

    [Fact]
    public async Task PlannerFailureMarksPlannerStageFailedSafe()
    {
        await using var db = CreateDbContext();
        var scenario = await CreateScenarioAsync(db);
        scenario.Planner.Succeed = false;
        var orchestrator = CreateOrchestrator(db, scenario, new FakeIntelligence());

        var result = await orchestrator.ExecuteAsync(scenario.Tourist.Id, scenario.Trip.Id);

        Assert.True(result.Error is not null);
        var workflow = await db.AgentWorkflows.Include(item => item.Stages).SingleAsync();
        var stage = Assert.Single(workflow.Stages);
        Assert.Equal(AgentWorkflowAgentRole.Planner, stage.AgentRole);
        Assert.Equal(AgentWorkflowStageStatus.Failed, stage.Status);
        Assert.Equal(AgentWorkflowStatus.FailedSafe, workflow.Status);
    }

    [Fact]
    public async Task DestinationNoResultsFailsSafelyWithoutFabricatingAttractions()
    {
        await using var db = CreateDbContext();
        var scenario = await CreateScenarioAsync(db);
        scenario.Destination.HasResults = false;
        var orchestrator = CreateOrchestrator(db, scenario, new FakeIntelligence());

        await orchestrator.ExecuteAsync(scenario.Tourist.Id, scenario.Trip.Id);

        var workflow = await db.AgentWorkflows.Include(item => item.Stages).SingleAsync();
        Assert.Equal(AgentWorkflowStatus.FailedSafe, workflow.Status);
        Assert.Equal(AgentWorkflowStageStatus.Failed, workflow.Stages.Single(stage => stage.AgentRole == AgentWorkflowAgentRole.Destination).Status);
        Assert.Empty(db.Itineraries);
    }

    [Fact]
    public async Task CrossUserTripIsRejectedBeforeWorkflowCreation()
    {
        await using var db = CreateDbContext();
        var scenario = await CreateScenarioAsync(db);
        var otherUser = new User { Id = Guid.NewGuid(), FirstName = "Other", LastName = "Tourist", Email = "other-workflow@example.com", PasswordHash = "hash", Role = UserRole.Tourist, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Users.Add(otherUser);
        await db.SaveChangesAsync();
        var orchestrator = CreateOrchestrator(db, scenario, new FakeIntelligence());

        var result = await orchestrator.ExecuteAsync(otherUser.Id, scenario.Trip.Id);

        Assert.True(result.NotFound);
        Assert.Empty(db.AgentWorkflows);
    }

    [Fact]
    public async Task ExistingActiveWorkflowPreventsDuplicateRun()
    {
        await using var db = CreateDbContext();
        var scenario = await CreateScenarioAsync(db);
        var persistence = new AgentWorkflowPersistenceService(db);
        await persistence.CreateAsync(Guid.NewGuid(), scenario.Trip.Id, scenario.Tourist.Id);
        await persistence.StartStageAsync((await db.AgentWorkflows.SingleAsync()).WorkflowId, AgentWorkflowAgentRole.Planner, 1);
        var orchestrator = CreateOrchestrator(db, scenario, new FakeIntelligence());

        var result = await orchestrator.ExecuteAsync(scenario.Tourist.Id, scenario.Trip.Id);

        Assert.Contains("active", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Single(db.AgentWorkflows);
    }

    private static AgentTripWorkflowOrchestrator CreateOrchestrator(ApplicationDbContext db, Scenario scenario, FakeIntelligence intelligence) =>
        new(
            db,
            scenario.Planner,
            new AttractionService(db),
            scenario.Destination,
            scenario.Booking,
            intelligence,
            new AgentWorkflowPersistenceService(db),
            NullLogger<AgentTripWorkflowOrchestrator>.Instance);

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Scenario> CreateScenarioAsync(ApplicationDbContext db)
    {
        var tourist = new User
        {
            Id = Guid.NewGuid(), FirstName = "Test", LastName = "Tourist", Email = $"{Guid.NewGuid()}@example.com",
            PasswordHash = "hash", Role = UserRole.Tourist, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var trip = new Trip
        {
            Id = Guid.NewGuid(), TouristId = tourist.Id, Name = "Workflow trip", StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 3), Budget = 10000m, Status = TripStatus.Draft, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var category = new Category { Id = Guid.NewGuid(), Name = "Culture" };
        var attraction = new Attraction
        {
            Id = Guid.NewGuid(), ProviderId = Guid.NewGuid(), CategoryId = category.Id, Category = category,
            Name = "Temple", Description = "Trusted attraction", District = "Kandy", Address = "Kandy",
            Price = 1000m, Status = "Approved", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        db.Users.Add(tourist);
        db.Trips.Add(trip);
        db.Categories.Add(category);
        db.Attractions.Add(attraction);
        await db.SaveChangesAsync();

        return new Scenario(tourist, trip, attraction, new FakePlanner(attraction.Id), new FakeDestination(attraction.Id), new FakeBooking());
    }

    private sealed record Scenario(User Tourist, Trip Trip, Attraction Attraction, FakePlanner Planner, FakeDestination Destination, FakeBooking Booking);

    private sealed class FakePlanner(Guid attractionId) : IPlannerAgentService
    {
        public bool Succeed { get; set; } = true;

        public Task<PlannerAgentServiceResult> GenerateAsync(PlannerAgentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Succeed
                ? new PlannerAgentServiceResult(Value: new PlannerAgentResponse(
                    [new PlannerDay(1, request.StartDate, [new PlannerItem(attractionId, new TimeOnly(9), new TimeOnly(11), 1000m, "Planner requirement")])],
                    1000m, "Generated", null))
                : new PlannerAgentServiceResult(Error: "planner unavailable", ServiceUnavailable: true));
    }

    private sealed class FakeDestination(Guid attractionId) : IDestinationAgentService
    {
        public bool HasResults { get; set; } = true;

        public Task<DestinationAgentServiceResult> RecommendAsync(DestinationRecommendationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(HasResults
                ? new DestinationAgentServiceResult(Value: new DestinationAgentResponse(
                    [new DestinationCandidateResponse(attractionId, "Temple", "Kandy", Guid.NewGuid(), "Culture", 1000m, [], [], ["interest_match"], 5)], "Success", null))
                : new DestinationAgentServiceResult(Value: new DestinationAgentResponse([], "NoResults", "No results")));
    }

    private sealed class FakeBooking : IBookingActionAgentService
    {
        public Task<BookingActionAgentServiceResult> PrepareAsync(BookingActionAgentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new BookingActionAgentServiceResult(Value: new BookingActionAgentResponse(
                request.WorkflowId, request.TripId, "NoEligibleProposal", [],
                [new BookingActionIssue("NoAvailability", "No M3 slot", null)], false, "No proposal.")));
    }

    private sealed class FakeIntelligence : IItineraryTravelIntelligenceWorkflowService
    {
        public bool ApprovalRequired { get; set; }
        public int CallCount { get; private set; }

        public Task<ItineraryTravelIntelligenceWorkflowResult> ProcessAsync(Guid tripId, Guid touristId, CancellationToken cancellationToken = default)
        {
            CallCount++;
            var validationId = Guid.NewGuid();
            return Task.FromResult(new ItineraryTravelIntelligenceWorkflowResult(
                true,
                null,
                validationId,
                Guid.NewGuid(),
                ApprovalRequired
                    ? new ApprovalRequestResponse { Id = Guid.NewGuid(), ValidationResultId = validationId, RequestedByUserId = touristId, Status = ApprovalRequestStatus.Pending, Summary = "Review required" }
                    : null));
        }
    }
}
