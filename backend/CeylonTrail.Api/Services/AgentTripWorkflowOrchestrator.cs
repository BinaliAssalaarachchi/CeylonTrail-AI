using System.Text.Json;
using System.Text.Json.Serialization;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.Attractions;
using CeylonTrail.Api.DTOs.BookingAction;
using CeylonTrail.Api.DTOs.Planner;
using CeylonTrail.Api.DTOs.Trips;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class AgentTripWorkflowOrchestrator(
    ApplicationDbContext dbContext,
    IPlannerAgentService plannerAgent,
    IAttractionService attractionService,
    IDestinationAgentService destinationAgent,
    IBookingActionAgentService bookingActionAgent,
    IItineraryTravelIntelligenceWorkflowService travelIntelligenceWorkflow,
    IAgentWorkflowPersistenceService workflowPersistence,
    ILogger<AgentTripWorkflowOrchestrator> logger) : IAgentTripWorkflowOrchestrator
{
    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<TripServiceResult<ItineraryResponse>> ExecuteAsync(
        Guid touristId,
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await dbContext.Trips
            .Include(candidate => candidate.Preferences)
            .SingleOrDefaultAsync(candidate => candidate.Id == tripId && candidate.TouristId == touristId, cancellationToken);
        if (trip is null)
        {
            return new(NotFound: true);
        }

        if (trip.Status is TripStatus.Completed or TripStatus.Cancelled)
        {
            return new(Error: $"A {trip.Status} trip cannot generate a new itinerary.");
        }

        var active = await workflowPersistence.GetActiveForTripAsync(tripId, touristId, cancellationToken);
        if (active.Succeeded)
        {
            return new(Error: "An active itinerary workflow already exists for this trip.");
        }

        var workflowId = Guid.NewGuid();
        var created = await workflowPersistence.CreateAsync(workflowId, tripId, touristId, cancellationToken);
        if (!created.Succeeded)
        {
            return new(Error: created.Error ?? "The itinerary workflow could not be created.");
        }

        var plannerStage = await workflowPersistence.StartStageAsync(
            workflowId,
            AgentWorkflowAgentRole.Planner,
            1,
            inputSnapshotJson: Serialize(new
            {
                tripId,
                startDate = trip.StartDate,
                endDate = trip.EndDate,
                trip.Budget,
                interests = trip.Preferences.Where(item => item.PreferenceType == "Interest").Select(item => item.Value).Take(30).ToList()
            }),
            cancellationToken: cancellationToken);
        if (!plannerStage.Succeeded)
        {
            return Failure(plannerStage.Error);
        }

        var candidates = await attractionService.SearchAsync(
            new AttractionSearchRequest
            {
                District = trip.Preferences.FirstOrDefault(item =>
                    string.Equals(item.PreferenceType, "Region", StringComparison.OrdinalIgnoreCase))?.Value,
                Page = 1,
                PageSize = 100,
                Sort = "name_asc"
            },
            touristId,
            cancellationToken);
        if (!candidates.Succeeded || candidates.Value is null || candidates.Value.Items.Count == 0)
        {
            return await FailStageAsync(workflowId, plannerStage.Stage!, "PlannerCandidatesUnavailable", candidates.Error ?? "No approved attraction candidates are available.", cancellationToken, serviceUnavailable: false);
        }

        var plannerRequest = new PlannerAgentRequest(
            trip.Id.ToString(),
            trip.StartDate,
            trip.EndDate,
            trip.EndDate.DayNumber - trip.StartDate.DayNumber + 1,
            trip.Budget,
            trip.Preferences.Where(item => string.Equals(item.PreferenceType, "Interest", StringComparison.OrdinalIgnoreCase)).Select(item => item.Value).ToList(),
            trip.Preferences.Where(item => string.Equals(item.PreferenceType, "Region", StringComparison.OrdinalIgnoreCase)).Select(item => item.Value).ToList(),
            trip.Preferences.Select(item => new PlannerPreference(item.PreferenceType, item.Value)).ToList(),
            candidates.Value.Items.Select(ToPlannerCandidate).ToList());

        PlannerAgentServiceResult plannerResult;
        try
        {
            plannerResult = await plannerAgent.GenerateAsync(plannerRequest, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Planner stage failed for workflow {WorkflowId}.", workflowId);
            return await FailStageAsync(workflowId, plannerStage.Stage!, "PlannerFailed", "Planner Agent failed safely.", cancellationToken);
        }

        if (!plannerResult.Succeeded || plannerResult.Value is null)
        {
            return await FailStageAsync(workflowId, plannerStage.Stage!, "PlannerFailed", plannerResult.Error ?? "Planner Agent failed safely.", cancellationToken);
        }

        var plannerOutput = plannerResult.Value;
        var candidatePrices = candidates.Value.Items.ToDictionary(candidate => candidate.Id, candidate => candidate.Price);
        var plannerError = ValidatePlannerOutput(plannerOutput, trip, candidatePrices);
        if (plannerError is not null)
        {
            return await FailStageAsync(workflowId, plannerStage.Stage!, "PlannerOutputInvalid", plannerError, cancellationToken);
        }

        await workflowPersistence.CompleteStageAsync(
            workflowId,
            plannerStage.Stage!.Id,
            Serialize(new
            {
                status = plannerOutput.Status,
                dayCount = plannerOutput.Days.Count,
                attractionIds = plannerOutput.Days.SelectMany(day => day.Items).Select(item => item.AttractionId).Distinct().ToList(),
                plannerOutput.EstimatedCost,
                trace = plannerOutput.Trace
            }),
            cancellationToken: cancellationToken);

        var destinationStage = await workflowPersistence.StartStageAsync(
            workflowId,
            AgentWorkflowAgentRole.Destination,
            2,
            inputSnapshotJson: Serialize(new
            {
                tripId,
                plannerAttractionIds = plannerOutput.Days.SelectMany(day => day.Items).Select(item => item.AttractionId).Distinct().ToList(),
                trip.Budget
            }),
            cancellationToken: cancellationToken);
        if (!destinationStage.Succeeded)
        {
            return Failure(destinationStage.Error);
        }

        DestinationAgentServiceResult destinationResult;
        try
        {
            destinationResult = await destinationAgent.RecommendAsync(new DestinationRecommendationRequest
            {
                Interests = trip.Preferences.Where(item => item.PreferenceType == "Interest").Select(item => item.Value).ToList(),
                District = trip.Preferences.FirstOrDefault(item => item.PreferenceType == "Region")?.Value,
                MaxBudget = trip.Budget,
                Date = null,
                Limit = 50
            }, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Destination stage failed for workflow {WorkflowId}.", workflowId);
            return await FailStageAsync(workflowId, destinationStage.Stage!, "DestinationFailed", "Destination Agent failed safely.", cancellationToken);
        }

        if (!destinationResult.Succeeded || destinationResult.Value is null)
        {
            return await FailStageAsync(workflowId, destinationStage.Stage!, "DestinationFailed", destinationResult.Error ?? "Destination Agent failed safely.", cancellationToken);
        }

        var destinationIds = destinationResult.Value.Candidates.Select(item => item.AttractionId).ToHashSet();
        var groundedDays = plannerOutput.Days
            .Select(day => new PlannerDay(
                day.DayNumber,
                day.Date,
                day.Items.Where(item => destinationIds.Contains(item.AttractionId)).ToList()))
            .Where(day => day.Items.Count > 0)
            .ToList();
        if (groundedDays.Count == 0)
        {
            return await FailStageAsync(workflowId, destinationStage.Stage!, "DestinationNoResults", "Destination Agent returned no attraction that grounded the Planner output.", cancellationToken, serviceUnavailable: false);
        }

        var groundedOutput = new PlannerAgentResponse(
            groundedDays,
            groundedDays.SelectMany(day => day.Items).Sum(item => item.EstimatedCost),
            "Generated",
            "Grounded by Destination Agent recommendations.");
        var groundedValidationError = ValidatePlannerOutput(groundedOutput, trip, candidatePrices);
        if (groundedValidationError is not null)
        {
            return await FailStageAsync(workflowId, destinationStage.Stage!, "DestinationOutputInvalid", groundedValidationError, cancellationToken);
        }

        await workflowPersistence.CompleteStageAsync(
            workflowId,
            destinationStage.Stage!.Id,
            Serialize(new
            {
                status = destinationResult.Value.Status,
                selectedAttractionIds = destinationIds.ToList(),
                groundedAttractionIds = groundedDays.SelectMany(day => day.Items).Select(item => item.AttractionId).Distinct().ToList(),
                trace = destinationResult.Value.Trace
            }),
            cancellationToken: cancellationToken);

        var bookingStage = await workflowPersistence.StartStageAsync(
            workflowId,
            AgentWorkflowAgentRole.BookingAction,
            3,
            inputSnapshotJson: Serialize(new
            {
                tripId,
                selectedAttractionIds = groundedDays.SelectMany(day => day.Items).Select(item => item.AttractionId).Distinct().ToList(),
                guestCount = 1,
                groundedOutput.EstimatedCost
            }),
            cancellationToken: cancellationToken);
        if (!bookingStage.Succeeded)
        {
            return Failure(bookingStage.Error);
        }

        var selectedAttractionIds = groundedDays.SelectMany(day => day.Items).Select(item => item.AttractionId).Distinct().ToList();
        BookingActionAgentServiceResult bookingResult;
        try
        {
            bookingResult = await bookingActionAgent.PrepareAsync(new BookingActionAgentRequest(
                workflowId,
                tripId,
                1,
                Math.Max(0m, trip.Budget - groundedOutput.EstimatedCost),
                selectedAttractionIds,
                trip.StartDate,
                trip.EndDate), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Booking Action stage failed for workflow {WorkflowId}.", workflowId);
            return await FailStageAsync(workflowId, bookingStage.Stage!, "BookingActionFailed", "Booking Action Agent failed safely.", cancellationToken);
        }

        if (!bookingResult.Succeeded || bookingResult.Value is null)
        {
            return await FailStageAsync(workflowId, bookingStage.Stage!, "BookingActionFailed", bookingResult.Error ?? "Booking Action Agent failed safely.", cancellationToken);
        }

        await workflowPersistence.CompleteStageAsync(
            workflowId,
            bookingStage.Stage!.Id,
            Serialize(new
            {
                contract = "CeylonTrail.BookingActionProposal.v1",
                workflowId = bookingResult.Value.WorkflowId,
                tripId = bookingResult.Value.TripId,
                status = bookingResult.Value.Status,
                proposals = bookingResult.Value.Proposals,
                issues = bookingResult.Value.Issues,
                requiresApproval = bookingResult.Value.Proposals.Count > 0,
                summary = bookingResult.Value.Summary,
                trace = bookingResult.Value.Trace
            }),
            cancellationToken: cancellationToken);

        var itinerary = await PersistItineraryAsync(trip, groundedOutput, cancellationToken);

        var intelligenceStage = await workflowPersistence.StartStageAsync(
            workflowId,
            AgentWorkflowAgentRole.TravelIntelligence,
            4,
            inputSnapshotJson: Serialize(new { tripId, itineraryId = itinerary.Id, groundedOutput.EstimatedCost }),
            cancellationToken: cancellationToken);
        if (!intelligenceStage.Succeeded)
        {
            return Failure(intelligenceStage.Error);
        }

        ItineraryTravelIntelligenceWorkflowResult intelligenceResult;
        try
        {
            intelligenceResult = await travelIntelligenceWorkflow.ProcessAsync(tripId, touristId, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Travel Intelligence stage failed for workflow {WorkflowId}.", workflowId);
            return await FailStageAsync(workflowId, intelligenceStage.Stage!, "TravelIntelligenceFailed", "Travel Intelligence failed safely.", cancellationToken);
        }

        if (!intelligenceResult.Succeeded || !intelligenceResult.ValidationResultId.HasValue || !intelligenceResult.ExecutionId.HasValue)
        {
            return await FailStageAsync(workflowId, intelligenceStage.Stage!, "TravelIntelligenceFailed", intelligenceResult.Error ?? "Travel Intelligence failed safely.", cancellationToken);
        }

        var approvalRequestId = intelligenceResult.ApprovalRequest?.Id;
        if (bookingResult.Value.Proposals.Count > 0 && !approvalRequestId.HasValue)
        {
            var bookingApproval = await CreateBookingApprovalRequestAsync(
                intelligenceResult.ValidationResultId.Value,
                intelligenceResult.ExecutionId.Value,
                touristId,
                bookingResult.Value.Proposals,
                cancellationToken);
            if (!bookingApproval.Succeeded)
            {
                return await FailStageAsync(
                    workflowId,
                    intelligenceStage.Stage!,
                    "BookingApprovalFailed",
                    bookingApproval.Error ?? "The booking approval request could not be created.",
                    cancellationToken);
            }

            approvalRequestId = bookingApproval.ApprovalRequestId;
        }

        await workflowPersistence.CompleteStageAsync(
            workflowId,
            intelligenceStage.Stage!.Id,
            Serialize(new
            {
                validationResultId = intelligenceResult.ValidationResultId,
                executionId = intelligenceResult.ExecutionId,
                approvalRequestId,
                requiresApproval = intelligenceResult.ApprovalRequest is not null
            }),
            intelligenceResult.ValidationResultId,
            intelligenceResult.ExecutionId,
            approvalRequestId,
            cancellationToken);

        if (approvalRequestId.HasValue)
        {
            await workflowPersistence.TransitionAsync(workflowId, AgentWorkflowStatus.AwaitingApproval, AgentWorkflowAgentRole.TravelIntelligence, cancellationToken: cancellationToken);
        }
        else
        {
            await workflowPersistence.TransitionAsync(workflowId, AgentWorkflowStatus.Completed, AgentWorkflowAgentRole.TravelIntelligence, cancellationToken: cancellationToken);
        }

        var responseAttractionIds = itinerary.Days.SelectMany(day => day.Items).Select(item => item.AttractionId).Distinct().ToList();
        var responseAttractions = await dbContext.Attractions.AsNoTracking()
            .Include(attraction => attraction.Category)
            .Where(attraction => responseAttractionIds.Contains(attraction.Id))
            .ToDictionaryAsync(attraction => attraction.Id, cancellationToken);
        return new(ToItineraryResponse(itinerary, responseAttractions));
    }

    private async Task<(bool Succeeded, Guid? ApprovalRequestId, string? Error)> CreateBookingApprovalRequestAsync(
        Guid validationResultId,
        Guid executionId,
        Guid touristId,
        IReadOnlyList<BookingActionProposal> proposals,
        CancellationToken cancellationToken)
    {
        var validation = await dbContext.ValidationResults
            .AsNoTracking()
            .SingleOrDefaultAsync(result => result.Id == validationResultId && result.CreatedByUserId == touristId, cancellationToken);
        if (validation is null)
        {
            return (false, null, "The booking approval could not be correlated to the itinerary validation.");
        }

        var existing = await dbContext.ApprovalRequests
            .SingleOrDefaultAsync(request =>
                request.ValidationResultId == validationResultId &&
                request.TravelIntelligenceExecutionId == executionId &&
                request.RequestedByUserId == touristId &&
                request.Status == ApprovalRequestStatus.Pending,
                cancellationToken);
        if (existing is not null)
        {
            return (true, existing.Id, null);
        }

        var now = DateTime.UtcNow;
        var approval = new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            ValidationResultId = validationResultId,
            TravelIntelligenceExecutionId = executionId,
            RequestedByUserId = touristId,
            Status = ApprovalRequestStatus.Pending,
            RecommendedAction = ApprovalRecommendedAction.Proceed,
            RiskLevel = validation.RiskLevel,
            Summary = $"BookingAction produced {proposals.Count} booking proposal(s). Coordinator approval is required before execution.",
            AffectedItemReferences = string.Join(", ", proposals.Select(proposal => proposal.AvailabilitySlotId).Distinct()),
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.ApprovalRequests.Add(approval);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (true, approval.Id, null);
    }

    private async Task<TripServiceResult<ItineraryResponse>> FailStageAsync(
        Guid workflowId,
        AgentWorkflowStage stage,
        string code,
        string message,
        CancellationToken cancellationToken,
        bool serviceUnavailable = true)
    {
        await workflowPersistence.FailStageAsync(workflowId, stage.Id, code, message, cancellationToken);
        return new(Error: message, ServiceUnavailable: serviceUnavailable);
    }

    private async Task<Itinerary> PersistItineraryAsync(Trip trip, PlannerAgentResponse output, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        foreach (var previous in await dbContext.Itineraries
                     .Where(itinerary => itinerary.TripId == trip.Id && itinerary.Status == ItineraryStatus.Active)
                     .ToListAsync(cancellationToken))
        {
            previous.Status = ItineraryStatus.Superseded;
            previous.UpdatedAt = now;
        }

        var itinerary = new Itinerary
        {
            Id = Guid.NewGuid(),
            TripId = trip.Id,
            Status = ItineraryStatus.Active,
            TotalEstimatedCost = output.EstimatedCost,
            CreatedAt = now,
            UpdatedAt = now
        };
        foreach (var plannerDay in output.Days)
        {
            var day = new ItineraryDay { Id = Guid.NewGuid(), DayNumber = plannerDay.DayNumber, Date = plannerDay.Date };
            foreach (var plannerItem in plannerDay.Items)
            {
                day.Items.Add(new ItineraryItem
                {
                    Id = Guid.NewGuid(),
                    AttractionId = plannerItem.AttractionId,
                    StartTime = plannerItem.StartTime,
                    EndTime = plannerItem.EndTime,
                    EstimatedCost = plannerItem.EstimatedCost,
                    Notes = plannerItem.Notes
                });
            }
            itinerary.Days.Add(day);
        }

        dbContext.Itineraries.Add(itinerary);
        if (trip.Status == TripStatus.Draft)
        {
            trip.Status = TripStatus.Planned;
        }
        trip.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return itinerary;
    }

    private static PlannerCandidateAttraction ToPlannerCandidate(AttractionResponse attraction) => new(
        attraction.Id.ToString(), attraction.Name, attraction.Category?.Name, attraction.District,
        attraction.Price, attraction.Description, FormatOpeningInformation(attraction));

    private static string? FormatOpeningInformation(AttractionResponse attraction)
    {
        var schedules = attraction.Schedules
            .Where(schedule => !schedule.IsClosed && schedule.OpeningTime.HasValue && schedule.ClosingTime.HasValue)
            .Select(schedule => $"{schedule.DayOfWeek}: {schedule.OpeningTime:HH\\:mm}-{schedule.ClosingTime:HH\\:mm}")
            .ToList();
        return schedules.Count == 0 ? null : string.Join("; ", schedules);
    }

    private static string? ValidatePlannerOutput(PlannerAgentResponse output, Trip trip, IReadOnlyDictionary<Guid, decimal> candidatePrices)
    {
        if (output.Status != "Generated" || output.Days.Count == 0)
            return output.Message ?? "Planner Agent did not produce an itinerary.";

        var dayNumbers = new HashSet<int>();
        var attractionIds = new HashSet<Guid>();
        decimal total = 0m;
        foreach (var day in output.Days)
        {
            if (day.DayNumber <= 0 || !dayNumbers.Add(day.DayNumber) || day.Date < trip.StartDate || day.Date > trip.EndDate)
                return "Planner Agent returned invalid day data.";
            TimeOnly? previousEnd = null;
            foreach (var item in day.Items.OrderBy(item => item.StartTime))
            {
                if (!candidatePrices.TryGetValue(item.AttractionId, out var price) || item.EstimatedCost != price || !attractionIds.Add(item.AttractionId) || item.EndTime <= item.StartTime || previousEnd.HasValue && item.StartTime < previousEnd.Value)
                    return "Planner Agent returned invalid attraction or schedule data.";
                previousEnd = item.EndTime;
                total += item.EstimatedCost;
            }
        }
        return total != output.EstimatedCost || total > trip.Budget ? "Planner Agent returned an invalid or over-budget itinerary." : null;
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, SnapshotOptions);

    private static TripServiceResult<ItineraryResponse> Failure(string? error) =>
        new(Error: error ?? "The itinerary workflow failed safely.", ServiceUnavailable: true);

    private static ItineraryResponse ToItineraryResponse(
        Itinerary itinerary,
        IReadOnlyDictionary<Guid, Attraction> attractions) => new(
        itinerary.Id, itinerary.TripId, itinerary.Status, itinerary.TotalEstimatedCost, itinerary.CreatedAt, itinerary.UpdatedAt,
        itinerary.Days.OrderBy(day => day.DayNumber).Select(day => new ItineraryDayResponse(
            day.Id, day.DayNumber, day.Date,
            day.Items.OrderBy(item => item.StartTime).Select(item => new ItineraryItemResponse(
                item.Id, item.AttractionId, item.StartTime, item.EndTime, item.EstimatedCost, item.Notes,
                attractions.TryGetValue(item.AttractionId, out var attraction) ? attraction.Name : null,
                attractions.TryGetValue(item.AttractionId, out attraction) ? attraction.Description : null,
                attractions.TryGetValue(item.AttractionId, out attraction) ? attraction.Address : null,
                attractions.TryGetValue(item.AttractionId, out attraction) ? attraction.District : null,
                attractions.TryGetValue(item.AttractionId, out attraction) ? attraction.Category?.Name : null,
                attractions.TryGetValue(item.AttractionId, out attraction) ? attraction.Latitude : null,
                attractions.TryGetValue(item.AttractionId, out attraction) ? attraction.Longitude : null)).ToList())).ToList());
}
