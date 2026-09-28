using System.Text.Json;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.BookingAction;
using CeylonTrail.Api.DTOs.Bookings;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class ApprovedWorkflowActionExecutorTests
{
    [Fact]
    public async Task ApproveValidProposalCreatesOneConfirmedBooking()
    {
        await using var db = CreateDbContext();
        var state = await CreateStateAsync(db);

        var result = await DecideAsync(db, state, ApprovalDecisionType.Approved);

        Assert.True(result.Succeeded);
        Assert.True(result.Response!.ExecutionSucceeded);
        var booking = Assert.Single(db.Bookings
            .Include(item => item.Items)
            .Include(item => item.StatusHistory));
        Assert.Equal(BookingStatus.Confirmed, booking.CurrentStatus);
        Assert.Contains(booking.StatusHistory, history =>
            history.PreviousStatus == BookingStatus.Draft &&
            history.NewStatus == BookingStatus.Confirmed);
        Assert.Equal(state.Trip.Id, booking.TripId);
        Assert.Equal(state.Tourist.Id, booking.UserId);
        Assert.Equal(1, booking.Items.Single().NumberOfGuests);
        Assert.Equal(state.Slot.PricePerPerson, booking.Items.Single().UnitPrice);
        Assert.Equal(AgentWorkflowStatus.Completed, db.AgentWorkflows.Single().Status);
    }

    [Fact]
    public async Task AlreadyApprovedWithoutExecutionCanResumeWithoutCreatingAnotherDecision()
    {
        await using var db = CreateDbContext();
        var state = await CreateStateAsync(db);
        var decision = await new ApprovalRequestService(db).DecideAsync(
            state.Approval.Id,
            state.Coordinator.Id,
            ApprovalDecisionType.Approved,
            null);

        var resumed = await DecideAsync(db, state, ApprovalDecisionType.Approved);

        Assert.True(decision.Succeeded);
        Assert.True(resumed.Succeeded);
        Assert.True(resumed.Response!.ExecutionSucceeded);
        Assert.Single(db.ApprovalDecisions);
        Assert.Single(db.Bookings);
    }

    [Fact]
    public async Task ApproveUsesCurrentServerPriceInsteadOfStaleProposalPrice()
    {
        await using var db = CreateDbContext();
        var state = await CreateStateAsync(db, proposalPrice: 1m, serverPrice: 55m);

        var result = await DecideAsync(db, state, ApprovalDecisionType.Approved);

        Assert.True(result.Response!.ExecutionSucceeded);
        Assert.Equal(55m, db.BookingItems.Single().UnitPrice);
        Assert.Equal(55m, db.Bookings.Single().TotalAmount);
    }

    [Fact]
    public async Task ApproveWithInsufficientCurrentCapacityCreatesNoBooking()
    {
        await using var db = CreateDbContext();
        var state = await CreateStateAsync(db, maxCapacity: 1, bookedCapacity: 1);

        var result = await DecideAsync(db, state, ApprovalDecisionType.Approved);

        Assert.False(result.Response!.ExecutionSucceeded);
        Assert.Empty(db.Bookings);
        Assert.Equal(AgentWorkflowStatus.FailedSafe, db.AgentWorkflows.Single().Status);
    }

    [Theory]
    [InlineData(false, "Approved")]
    [InlineData(true, "PendingApproval")]
    public async Task ApproveWhenAttractionIsNotBookableCreatesNoBooking(bool active, string status)
    {
        await using var db = CreateDbContext();
        var state = await CreateStateAsync(db);
        state.Attraction.IsActive = active;
        state.Attraction.Status = status;
        await db.SaveChangesAsync();

        var result = await DecideAsync(db, state, ApprovalDecisionType.Approved);

        Assert.False(result.Response!.ExecutionSucceeded);
        Assert.Empty(db.Bookings);
    }

    [Fact]
    public async Task ApproveWhenSlotIsPastCreatesNoBooking()
    {
        await using var db = CreateDbContext();
        var state = await CreateStateAsync(db);
        state.Slot.StartTime = DateTime.UtcNow.AddHours(-2);
        state.Slot.EndTime = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        var result = await DecideAsync(db, state, ApprovalDecisionType.Approved);

        Assert.False(result.Response!.ExecutionSucceeded);
        Assert.Empty(db.Bookings);
    }

    [Fact]
    public async Task MalformedBookingActionSnapshotCreatesNoBooking()
    {
        await using var db = CreateDbContext();
        var state = await CreateStateAsync(db);
        state.BookingStage.OutputSnapshotJson = "{not-json";
        await db.SaveChangesAsync();

        var result = await DecideAsync(db, state, ApprovalDecisionType.Approved);

        Assert.False(result.Response!.ExecutionSucceeded);
        Assert.Empty(db.Bookings);
        Assert.Equal(AgentWorkflowStatus.FailedSafe, db.AgentWorkflows.Single().Status);
    }

    [Fact]
    public async Task WorkflowAndProposalMismatchCreatesNoBooking()
    {
        await using var db = CreateDbContext();
        var state = await CreateStateAsync(db);
        state.BookingStage.OutputSnapshotJson = Snapshot(
            state.Workflow.WorkflowId,
            Guid.NewGuid(),
            state.Attraction.Id,
            state.Slot.Id,
            state.Slot.PricePerPerson);
        await db.SaveChangesAsync();

        var result = await DecideAsync(db, state, ApprovalDecisionType.Approved);

        Assert.False(result.Response!.ExecutionSucceeded);
        Assert.Empty(db.Bookings);
    }

    [Fact]
    public async Task IncompleteBookingActionStageCreatesNoBooking()
    {
        await using var db = CreateDbContext();
        var state = await CreateStateAsync(db);
        state.BookingStage.Status = AgentWorkflowStageStatus.Running;
        await db.SaveChangesAsync();

        var result = await DecideAsync(db, state, ApprovalDecisionType.Approved);

        Assert.False(result.Response!.ExecutionSucceeded);
        Assert.Empty(db.Bookings);
    }

    [Fact]
    public async Task WorkflowNotAwaitingApprovalCreatesNoBooking()
    {
        await using var db = CreateDbContext();
        var state = await CreateStateAsync(db);
        state.Workflow.Status = AgentWorkflowStatus.Running;
        await db.SaveChangesAsync();

        var result = await DecideAsync(db, state, ApprovalDecisionType.Approved);

        Assert.False(result.Response!.ExecutionSucceeded);
        Assert.Empty(db.Bookings);
    }

    [Fact]
    public async Task RepeatedApproveDoesNotCreateDuplicateBooking()
    {
        await using var db = CreateDbContext();
        var state = await CreateStateAsync(db);
        var first = await DecideAsync(db, state, ApprovalDecisionType.Approved);
        var second = await DecideAsync(db, state, ApprovalDecisionType.Approved);

        Assert.True(first.Response!.ExecutionSucceeded);
        Assert.True(second.Succeeded);
        Assert.Equal(first.Response.BookingId, second.Response!.BookingId);
        Assert.Single(db.Bookings);
    }

    [Fact]
    public async Task RetryWithPartiallyCreatedDraftFinishesTheSameBooking()
    {
        await using var db = CreateDbContext();
        var state = await CreateStateAsync(db);
        var draft = await new BookingService(db).CreateBookingAsync(
            state.Tourist.Id,
            new CreateBookingRequest
            {
                TripId = state.Trip.Id,
                Items = new()
                {
                    new BookingItemRequest
                    {
                        AvailabilitySlotId = state.Slot.Id,
                        NumberOfGuests = 1
                    }
                }
            });

        Assert.True(draft.Succeeded);
        var result = await DecideAsync(db, state, ApprovalDecisionType.Approved);

        Assert.True(result.Response!.ExecutionSucceeded);
        var booking = Assert.Single(db.Bookings.Include(item => item.StatusHistory));
        Assert.Equal(draft.Response!.Id, booking.Id);
        Assert.Equal(BookingStatus.Confirmed, booking.CurrentStatus);
        Assert.Contains(booking.StatusHistory, history =>
            history.PreviousStatus == BookingStatus.Draft &&
            history.NewStatus == BookingStatus.Confirmed);
    }

    [Fact]
    public async Task FailedApprovedExecutionRemainsFailedAndDoesNotCreateBookingOnRetry()
    {
        await using var db = CreateDbContext();
        var state = await CreateStateAsync(db, maxCapacity: 1, bookedCapacity: 1);

        var first = await DecideAsync(db, state, ApprovalDecisionType.Approved);
        var second = await DecideAsync(db, state, ApprovalDecisionType.Approved);

        Assert.False(first.Response!.ExecutionSucceeded);
        Assert.True(second.Succeeded);
        Assert.False(second.Response!.ExecutionSucceeded);
        Assert.Single(db.ApprovalDecisions);
        Assert.Empty(db.Bookings);
        Assert.Equal(AgentWorkflowStatus.FailedSafe, db.AgentWorkflows.Single().Status);
    }

    [Fact]
    public async Task RejectCreatesNoBookingAndTerminalizesWorkflow()
    {
        await using var db = CreateDbContext();
        var state = await CreateStateAsync(db);

        var result = await DecideAsync(db, state, ApprovalDecisionType.Rejected);

        Assert.True(result.Succeeded);
        Assert.Empty(db.Bookings);
        Assert.Equal(ApprovalRequestStatus.Rejected, db.ApprovalRequests.Single().Status);
        Assert.Equal(AgentWorkflowStatus.Cancelled, db.AgentWorkflows.Single().Status);
    }

    private static async Task<(bool Succeeded, string? Error, CeylonTrail.Api.DTOs.ApprovalRequests.ApprovalRequestResponse? Response)> DecideAsync(
        ApplicationDbContext db,
        WorkflowState state,
        ApprovalDecisionType decision)
    {
        var executor = new ApprovedWorkflowActionExecutor(
            db,
            new BookingService(db),
            NullLogger<ApprovedWorkflowActionExecutor>.Instance);
        var service = new ApprovalRequestService(db, executor);
        return await service.DecideAsync(state.Approval.Id, state.Coordinator.Id, decision, null);
    }

    private static async Task<WorkflowState> CreateStateAsync(
        ApplicationDbContext db,
        decimal proposalPrice = 50m,
        decimal serverPrice = 50m,
        int maxCapacity = 5,
        int bookedCapacity = 0)
    {
        var now = DateTime.UtcNow;
        var tourist = User(UserRole.Tourist, "tourist");
        var coordinator = User(UserRole.TravelCoordinator, "coordinator");
        var category = new Category { Id = Guid.NewGuid(), Name = $"Category-{Guid.NewGuid()}" };
        var attraction = new Attraction
        {
            Id = Guid.NewGuid(), ProviderId = Guid.NewGuid(), CategoryId = category.Id, Category = category,
            Name = "Approved attraction", Description = "Description", District = "Kandy", Address = "Kandy",
            Status = "Approved", IsActive = true, Price = serverPrice, CreatedAt = now, UpdatedAt = now
        };
        var startDate = DateOnly.FromDateTime(now.AddDays(1));
        var trip = new Trip
        {
            Id = Guid.NewGuid(), TouristId = tourist.Id, Name = "Approval trip", StartDate = startDate,
            EndDate = startDate.AddDays(3), Budget = 1000m, Status = TripStatus.Planned, CreatedAt = now, UpdatedAt = now
        };
        var slot = new AvailabilitySlot
        {
            Id = Guid.NewGuid(), AttractionId = attraction.Id, Attraction = attraction,
            StartTime = now.AddDays(1), EndTime = now.AddDays(1).AddHours(2), MaxCapacity = maxCapacity,
            BookedCapacity = bookedCapacity, PricePerPerson = serverPrice, CreatedAt = now, UpdatedAt = now
        };
        var workflow = new AgentWorkflow
        {
            Id = Guid.NewGuid(), WorkflowId = Guid.NewGuid(), TripId = trip.Id, Trip = trip,
            RequestedByUserId = tourist.Id, RequestedByUser = tourist, Status = AgentWorkflowStatus.AwaitingApproval,
            CurrentStage = AgentWorkflowAgentRole.TravelIntelligence, StartedAt = now, UpdatedAt = now
        };
        var validation = new ValidationResult
        {
            Id = Guid.NewGuid(), CreatedByUserId = tourist.Id, TripReference = trip.Id.ToString(),
            OverallStatus = ValidationOverallStatus.Valid, RiskLevel = ValidationRiskLevel.High, IsFeasible = true,
            CreatedAt = now
        };
        var approval = new ApprovalRequest
        {
            Id = Guid.NewGuid(), ValidationResultId = validation.Id, ValidationResult = validation,
            RequestedByUserId = tourist.Id, Status = ApprovalRequestStatus.Pending,
            RecommendedAction = ApprovalRecommendedAction.Proceed, RiskLevel = ValidationRiskLevel.High,
            Summary = "Review booking", CreatedAt = now, UpdatedAt = now
        };
        var bookingStage = new AgentWorkflowStage
        {
            Id = Guid.NewGuid(), AgentWorkflowId = workflow.Id, AgentWorkflow = workflow,
            AgentRole = AgentWorkflowAgentRole.BookingAction, Sequence = 3,
            Status = AgentWorkflowStageStatus.Completed, StartedAt = now, CompletedAt = now,
            OutputSnapshotJson = Snapshot(workflow.WorkflowId, trip.Id, attraction.Id, slot.Id, proposalPrice)
        };
        var approvalStage = new AgentWorkflowStage
        {
            Id = Guid.NewGuid(), AgentWorkflowId = workflow.Id, AgentWorkflow = workflow,
            AgentRole = AgentWorkflowAgentRole.TravelIntelligence, Sequence = 4,
            Status = AgentWorkflowStageStatus.Completed, ApprovalRequestId = approval.Id,
            StartedAt = now, CompletedAt = now
        };

        db.Users.AddRange(tourist, coordinator);
        db.Categories.Add(category);
        db.Attractions.Add(attraction);
        db.Trips.Add(trip);
        db.AvailabilitySlots.Add(slot);
        db.ValidationResults.Add(validation);
        db.ApprovalRequests.Add(approval);
        db.AgentWorkflows.Add(workflow);
        db.AgentWorkflowStages.AddRange(bookingStage, approvalStage);
        await db.SaveChangesAsync();
        return new WorkflowState(tourist, coordinator, trip, attraction, slot, workflow, approval, bookingStage);
    }

    private static string Snapshot(Guid workflowId, Guid tripId, Guid attractionId, Guid slotId, decimal unitPrice) =>
        JsonSerializer.Serialize(new
        {
            contract = "CeylonTrail.BookingActionProposal.v1",
            workflowId,
            tripId,
            status = "Prepared",
            proposals = new[]
            {
                new BookingActionProposal(attractionId, slotId, 1, unitPrice, unitPrice, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(2), "approved")
            },
            issues = Array.Empty<BookingActionIssue>(),
            requiresApproval = true,
            summary = "approved"
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static User User(UserRole role, string prefix) => new()
    {
        Id = Guid.NewGuid(), FirstName = prefix, LastName = "test", Email = $"{Guid.NewGuid()}@example.com",
        PasswordHash = "hash", Role = role, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
    };

    private static ApplicationDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed record WorkflowState(
        User Tourist,
        User Coordinator,
        Trip Trip,
        Attraction Attraction,
        AvailabilitySlot Slot,
        AgentWorkflow Workflow,
        ApprovalRequest Approval,
        AgentWorkflowStage BookingStage);
}
