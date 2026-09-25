using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Attractions;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.DTOs.Planner;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class ItineraryTravelIntelligenceWorkflowServiceTests
{
    [Fact]
    public async Task GenerateItinerary_TriggersTrustedValidationAndTravelIntelligenceAfterPersistence()
    {
        await using var dbContext = CreateDbContext();
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId);
        var attraction = await SeedAttractionAsync(dbContext);
        var workflow = new RecordingWorkflow();
        var service = new TripService(
            dbContext,
            new StubPlannerAgent(_ => GeneratedPlan(attraction.Id, trip.StartDate)),
            new AttractionService(dbContext),
            workflow,
            NullLogger<TripService>.Instance);

        var result = await service.GenerateItineraryAsync(touristId, trip.Id);

        Assert.Null(result.Error);
        Assert.True(workflow.Called);
        Assert.Equal(trip.Id, workflow.TripId);
        Assert.Equal(touristId, workflow.TouristId);
        Assert.True(await dbContext.Itineraries.AnyAsync(itinerary => itinerary.TripId == trip.Id));
    }

    [Fact]
    public async Task Workflow_UsesOwnerScopedPersistedItineraryAndPersistsExecution()
    {
        await using var dbContext = CreateDbContext();
        var touristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, touristId);
        var attraction = await SeedAttractionAsync(dbContext);
        dbContext.Itineraries.Add(new Itinerary
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            Status = ItineraryStatus.Active,
            TotalEstimatedCost = 2500m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Days =
            {
                new ItineraryDay
                {
                    Id = Guid.NewGuid(),
                    DayNumber = 1,
                    Date = trip.StartDate,
                    Items =
                    {
                        new ItineraryItem
                        {
                            Id = Guid.NewGuid(),
                            AttractionId = attraction.Id,
                            StartTime = new TimeOnly(9),
                            EndTime = new TimeOnly(11),
                            EstimatedCost = 2500m
                        }
                    }
                }
            }
        });
        await dbContext.SaveChangesAsync();

        var validation = new RecordingValidationService();
        var intelligence = new RecordingTravelIntelligenceService();
        var persistence = new RecordingPersistenceService();
        var workflow = new ItineraryTravelIntelligenceWorkflowService(
            dbContext,
            validation,
            intelligence,
            persistence);

        var result = await workflow.ProcessAsync(trip.Id, touristId);

        Assert.True(result.Succeeded);
        Assert.Equal(trip.Id.ToString(), validation.Request!.TripReference);
        Assert.Equal(trip.Budget, validation.Request.Budget);
        Assert.Equal(2500m, validation.Request.EstimatedCost);
        var item = Assert.Single(validation.Request.Items);
        Assert.Equal(attraction.Name, item.Title);
        Assert.Equal(attraction.District, item.District);
        Assert.Equal(validation.Response!.Id, intelligence.ValidationResultId);
        Assert.Equal(validation.Response.Id, persistence.ValidationResultId);
        Assert.True(persistence.ReceivedRecommendation);
    }

    [Fact]
    public async Task Workflow_DoesNotAnalyzeAnotherTouristsTrip()
    {
        await using var dbContext = CreateDbContext();
        var ownerId = Guid.NewGuid();
        var otherTouristId = Guid.NewGuid();
        var trip = await SeedTripAsync(dbContext, ownerId);
        var validation = new RecordingValidationService();
        var intelligence = new RecordingTravelIntelligenceService();
        var persistence = new RecordingPersistenceService();
        var workflow = new ItineraryTravelIntelligenceWorkflowService(dbContext, validation, intelligence, persistence);

        var result = await workflow.ProcessAsync(trip.Id, otherTouristId);

        Assert.False(result.Succeeded);
        Assert.False(validation.Called);
        Assert.False(intelligence.Called);
        Assert.False(persistence.Called);
    }

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Trip> SeedTripAsync(ApplicationDbContext dbContext, Guid touristId)
    {
        var trip = new Trip
        {
            Id = Guid.NewGuid(), TouristId = touristId, Name = "Workflow trip",
            StartDate = new DateOnly(2026, 10, 1), EndDate = new DateOnly(2026, 10, 5),
            Budget = 100000m, Status = TripStatus.Draft,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        dbContext.Trips.Add(trip);
        await dbContext.SaveChangesAsync();
        return trip;
    }

    private static async Task<Attraction> SeedAttractionAsync(ApplicationDbContext dbContext)
    {
        var category = new Category { Id = Guid.NewGuid(), Name = "Culture" };
        var attraction = new Attraction
        {
            Id = Guid.NewGuid(), ProviderId = Guid.NewGuid(), CategoryId = category.Id, Category = category,
            Name = "Temple", Description = "A trusted attraction.", District = "Kandy",
            Address = "Central Sri Lanka", Price = 2500m, Status = "Approved", IsActive = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        dbContext.Attractions.Add(attraction);
        await dbContext.SaveChangesAsync();
        return attraction;
    }

    private static PlannerAgentResponse GeneratedPlan(Guid attractionId, DateOnly date) => new(
        new[] { new PlannerDay(1, date, new[] { new PlannerItem(attractionId, new TimeOnly(9), new TimeOnly(11), 2500m, "Trusted plan") }) },
        2500m,
        "Generated",
        null);

    private sealed class StubPlannerAgent(Func<PlannerAgentRequest, PlannerAgentResponse> generate) : IPlannerAgentService
    {
        public Task<PlannerAgentServiceResult> GenerateAsync(PlannerAgentRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PlannerAgentServiceResult(Value: generate(request)));
    }

    private sealed class RecordingWorkflow : IItineraryTravelIntelligenceWorkflowService
    {
        public bool Called { get; private set; }
        public Guid TripId { get; private set; }
        public Guid TouristId { get; private set; }

        public Task<ItineraryTravelIntelligenceWorkflowResult> ProcessAsync(Guid tripId, Guid touristId, CancellationToken cancellationToken = default)
        {
            Called = true;
            TripId = tripId;
            TouristId = touristId;
            return Task.FromResult(new ItineraryTravelIntelligenceWorkflowResult(true, null, Guid.NewGuid(), Guid.NewGuid(), null));
        }
    }

    private sealed class RecordingValidationService : IItineraryValidationService
    {
        public bool Called { get; private set; }
        public ItineraryValidationRequest? Request { get; private set; }
        public ItineraryValidationResponse? Response { get; } = new()
        {
            Id = Guid.NewGuid(), TripReference = "", IsFeasible = true,
            OverallStatus = ValidationOverallStatus.Valid, RiskLevel = ValidationRiskLevel.Low
        };

        public Task<(bool Succeeded, string? Error, ItineraryValidationResponse? Response)> ValidateAsync(ItineraryValidationRequest request, Guid authenticatedUserId, CancellationToken cancellationToken = default)
        {
            Called = true;
            Request = request;
            Response!.TripReference = request.TripReference;
            Response.CreatedByUserId = authenticatedUserId;
            return Task.FromResult<(bool Succeeded, string? Error, ItineraryValidationResponse? Response)>(
                (true, null, Response));
        }

        public Task<ItineraryValidationResponse?> GetByIdAsync(Guid id, Guid authenticatedUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ItineraryValidationResponse?>(null);
    }

    private sealed class RecordingTravelIntelligenceService : ITravelIntelligenceService
    {
        public bool Called { get; private set; }
        public Guid ValidationResultId { get; private set; }

        public Task<TravelIntelligenceResponse> AnalyzeAsync(ItineraryValidationResponse validation, CancellationToken cancellationToken = default)
        {
            Called = true;
            ValidationResultId = validation.Id;
            return Task.FromResult(new TravelIntelligenceResponse
            {
                ValidationResultId = validation.Id,
                IsFeasible = validation.IsFeasible,
                RiskLevel = validation.RiskLevel,
                RecommendedAction = TravelIntelligenceAction.Proceed,
                Execution = new TravelIntelligenceExecutionMetadata
                {
                    AgentName = "TestAgent",
                    AgentVersion = "test",
                    ExecutionStatus = "Completed",
                    Provider = "test"
                }
            });
        }
    }

    private sealed class RecordingPersistenceService : ITravelIntelligenceExecutionPersistenceService
    {
        public bool Called { get; private set; }
        public bool ReceivedRecommendation { get; private set; }
        public Guid ValidationResultId { get; private set; }

        public Task<TravelIntelligenceExecutionPersistenceResult> PersistAsync(ItineraryValidationResponse validation, Guid requestedByUserId, TravelIntelligenceResponse recommendation, CancellationToken cancellationToken = default)
        {
            Called = true;
            ReceivedRecommendation = recommendation.ValidationResultId == validation.Id;
            ValidationResultId = validation.Id;
            return Task.FromResult(new TravelIntelligenceExecutionPersistenceResult(true, null, Guid.NewGuid(), null));
        }
    }
}
