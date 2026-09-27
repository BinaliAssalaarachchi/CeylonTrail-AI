using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.BookingAction;
using CeylonTrail.Api.DTOs.Bookings;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CeylonTrail.Api.Services;

public sealed class ApprovedWorkflowActionExecutor(
    ApplicationDbContext dbContext,
    IBookingService bookingService,
    ILogger<ApprovedWorkflowActionExecutor> logger) : IApprovedWorkflowActionExecutor
{
    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<ApprovedWorkflowActionExecutionResult> ExecuteAsync(
        ApprovalRequest approvalRequest,
        Guid decidedByUserId,
        CancellationToken cancellationToken = default)
    {
        var correlation = await LoadCorrelationAsync(approvalRequest, cancellationToken);
        if (correlation is null)
        {
            return await FailAsync(null, null, "ApprovalRequest is not correlated to an AgentWorkflow.", cancellationToken);
        }

        var (workflow, approvalStage, bookingStage) = correlation.Value;
        if (approvalRequest.RequestedByUserId != workflow.RequestedByUserId)
        {
            return await FailAsync(workflow, bookingStage, "The approval requester does not match the workflow owner.", cancellationToken);
        }
        if (workflow.Status == AgentWorkflowStatus.Completed &&
            TryGetExecutionBookingId(bookingStage.OutputSnapshotJson, out var existingBookingId))
        {
            return new(true, BookingId: existingBookingId, AlreadyExecuted: true);
        }

        if (workflow.Status != AgentWorkflowStatus.AwaitingApproval)
        {
            return await FailAsync(workflow, bookingStage, "The AgentWorkflow is not awaiting approval.", cancellationToken);
        }

        if (approvalStage.Status != AgentWorkflowStageStatus.Completed ||
            bookingStage.Status != AgentWorkflowStageStatus.Completed)
        {
            return await FailAsync(workflow, bookingStage, "The correlated workflow stages are not complete.", cancellationToken);
        }

        if (approvalRequest.Status != ApprovalRequestStatus.Approved ||
            approvalRequest.Decision?.Decision != ApprovalDecisionType.Approved ||
            approvalRequest.Decision.DecidedByUserId != decidedByUserId)
        {
            return await FailAsync(workflow, bookingStage, "The approval decision could not be verified.", cancellationToken);
        }

        BookingActionSnapshot? proposalSnapshot;
        try
        {
            proposalSnapshot = JsonSerializer.Deserialize<BookingActionSnapshot>(
                bookingStage.OutputSnapshotJson ?? string.Empty,
                SnapshotOptions);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Malformed BookingAction snapshot for approval {ApprovalRequestId}.", approvalRequest.Id);
            return await FailAsync(workflow, bookingStage, "The BookingAction proposal snapshot is malformed.", cancellationToken);
        }

        var snapshotError = ValidateSnapshot(proposalSnapshot, workflow);
        if (snapshotError is not null)
        {
            return await FailAsync(workflow, bookingStage, snapshotError, cancellationToken);
        }

        var trip = await dbContext.Trips
            .SingleOrDefaultAsync(candidate => candidate.Id == workflow.TripId, cancellationToken);
        if (trip is null || trip.TouristId != workflow.RequestedByUserId || trip.Status is TripStatus.Completed or TripStatus.Cancelled)
        {
            return await FailAsync(workflow, bookingStage, "The trip is no longer eligible for booking.", cancellationToken);
        }

        var proposals = proposalSnapshot!.Proposals!;
        var slots = await dbContext.AvailabilitySlots
            .Include(slot => slot.Attraction)
            .Where(slot => proposals.Select(proposal => proposal.AvailabilitySlotId).Contains(slot.Id))
            .ToListAsync(cancellationToken);
        var slotsById = slots.ToDictionary(slot => slot.Id);
        var authoritativeTotal = 0m;
        foreach (var proposal in proposals)
        {
            if (!slotsById.TryGetValue(proposal.AvailabilitySlotId, out var slot) ||
                slot.Attraction is null ||
                slot.AttractionId != proposal.AttractionId ||
                !slot.Attraction.IsActive ||
                !string.Equals(slot.Attraction.Status, "Approved", StringComparison.Ordinal) ||
                slot.StartTime <= DateTime.UtcNow ||
                slot.EndTime <= DateTime.UtcNow ||
                DateOnly.FromDateTime(slot.StartTime.ToUniversalTime()) < trip.StartDate ||
                DateOnly.FromDateTime(slot.EndTime.ToUniversalTime()) > trip.EndDate ||
                slot.BookedCapacity < 0 ||
                slot.MaxCapacity <= 0 ||
                slot.BookedCapacity > slot.MaxCapacity ||
                proposal.GuestCount > slot.MaxCapacity - slot.BookedCapacity)
            {
                return await FailAsync(workflow, bookingStage, "The approved proposal is no longer valid against current booking data.", cancellationToken);
            }

            authoritativeTotal += slot.PricePerPerson * proposal.GuestCount;
        }

        if (authoritativeTotal > trip.Budget)
        {
            return await FailAsync(workflow, bookingStage, "The current authoritative booking price exceeds the trip budget.", cancellationToken);
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        try
        {
            var bookingResult = await bookingService.CreateBookingAsync(
                workflow.RequestedByUserId,
                new CreateBookingRequest
                {
                    TripId = workflow.TripId,
                    Items = proposals.Select(proposal => new BookingItemRequest
                    {
                        AvailabilitySlotId = proposal.AvailabilitySlotId,
                        NumberOfGuests = proposal.GuestCount
                    }).ToList()
                },
                cancellationToken);
            if (!bookingResult.Succeeded || bookingResult.Response is null)
            {
                dbContext.ChangeTracker.Clear();
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return await FailAsync(workflow, bookingStage, bookingResult.Error ?? "Authoritative booking creation failed.", cancellationToken);
            }

            var accepted = await bookingService.AcceptBookingAsync(
                bookingResult.Response.Id,
                decidedByUserId,
                cancellationToken: cancellationToken);
            if (!accepted.Succeeded || accepted.Response is null)
            {
                dbContext.ChangeTracker.Clear();
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return await FailAsync(
                    workflow,
                    bookingStage,
                    accepted.Error ?? "Authoritative booking confirmation failed.",
                    cancellationToken);
            }

            var trackedStage = await dbContext.AgentWorkflowStages
                .SingleAsync(stage => stage.Id == bookingStage.Id, cancellationToken);
            trackedStage.OutputSnapshotJson = AddExecutionAudit(
                trackedStage.OutputSnapshotJson,
                true,
                null,
                accepted.Response.Id,
                decidedByUserId);
            var trackedWorkflow = await dbContext.AgentWorkflows
                .SingleAsync(candidate => candidate.Id == workflow.Id, cancellationToken);
            trackedWorkflow.Status = AgentWorkflowStatus.Completed;
            trackedWorkflow.CurrentStage = AgentWorkflowAgentRole.TravelIntelligence;
            trackedWorkflow.CompletedAt = DateTime.UtcNow;
            trackedWorkflow.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return new(true, BookingId: accepted.Response.Id);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception, "Booking capacity concurrency failed for approval {ApprovalRequestId}.", approvalRequest.Id);
            dbContext.ChangeTracker.Clear();
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return await FailAsync(workflow, bookingStage, "The selected availability changed during booking execution.", cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Approved booking execution failed for approval {ApprovalRequestId}.", approvalRequest.Id);
            dbContext.ChangeTracker.Clear();
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return await FailAsync(workflow, bookingStage, "The approved booking could not be executed safely.", cancellationToken);
        }
    }

    private async Task<(AgentWorkflow Workflow, AgentWorkflowStage ApprovalStage, AgentWorkflowStage BookingStage)?> LoadCorrelationAsync(
        ApprovalRequest approvalRequest,
        CancellationToken cancellationToken)
    {
        var approvalStage = await dbContext.AgentWorkflowStages
            .Include(stage => stage.AgentWorkflow)
            .SingleOrDefaultAsync(stage =>
                stage.ApprovalRequestId == approvalRequest.Id &&
                stage.AgentRole == AgentWorkflowAgentRole.TravelIntelligence,
                cancellationToken);
        if (approvalStage?.AgentWorkflow is null)
        {
            return null;
        }

        if (approvalRequest.TravelIntelligenceExecutionId.HasValue &&
            approvalStage.TravelIntelligenceExecutionId != approvalRequest.TravelIntelligenceExecutionId)
        {
            return null;
        }

        var bookingStage = await dbContext.AgentWorkflowStages
            .SingleOrDefaultAsync(stage =>
                stage.AgentWorkflowId == approvalStage.AgentWorkflowId &&
                stage.AgentRole == AgentWorkflowAgentRole.BookingAction,
                cancellationToken);
        return bookingStage is null ? null : (approvalStage.AgentWorkflow, approvalStage, bookingStage);
    }

    private async Task<ApprovedWorkflowActionExecutionResult> FailAsync(
        AgentWorkflow? workflow,
        AgentWorkflowStage? bookingStage,
        string error,
        CancellationToken cancellationToken)
    {
        if (workflow is null || bookingStage is null)
        {
            return new(false, error);
        }

        var trackedStage = await dbContext.AgentWorkflowStages
            .SingleAsync(stage => stage.Id == bookingStage.Id, cancellationToken);
        trackedStage.OutputSnapshotJson = AddExecutionAudit(
            trackedStage.OutputSnapshotJson,
            false,
            error,
            null,
            null);
        var trackedWorkflow = await dbContext.AgentWorkflows
            .SingleAsync(candidate => candidate.Id == workflow.Id, cancellationToken);
        trackedWorkflow.Status = AgentWorkflowStatus.FailedSafe;
        trackedWorkflow.FailureCode = "ApprovedBookingExecutionFailed";
        trackedWorkflow.FailureSummary = error.Length <= 1000 ? error : error[..1000];
        trackedWorkflow.CompletedAt = DateTime.UtcNow;
        trackedWorkflow.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(false, error);
    }

    private static string? ValidateSnapshot(BookingActionSnapshot? snapshot, AgentWorkflow workflow)
    {
        if (snapshot is null || snapshot.Contract != "CeylonTrail.BookingActionProposal.v1")
            return "The BookingAction proposal contract is invalid.";
        if (snapshot.WorkflowId != workflow.WorkflowId || snapshot.TripId != workflow.TripId)
            return "The BookingAction proposal is correlated to a different workflow or trip.";
        if (snapshot.Status != "Prepared" || !snapshot.RequiresApproval || snapshot.Proposals is null || snapshot.Proposals.Count == 0)
            return "The BookingAction stage does not contain an executable proposal.";
        if (snapshot.Proposals.Count > 100 || snapshot.Proposals.Any(proposal => proposal.GuestCount <= 0 || proposal.AttractionId == Guid.Empty || proposal.AvailabilitySlotId == Guid.Empty))
            return "The BookingAction proposal contains invalid identifiers or guest counts.";
        if (snapshot.Proposals.Select(proposal => proposal.AvailabilitySlotId).Distinct().Count() != snapshot.Proposals.Count)
            return "The BookingAction proposal contains duplicate availability slots.";
        return null;
    }

    private static bool TryGetExecutionBookingId(string? snapshotJson, out Guid bookingId)
    {
        bookingId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(snapshotJson))
            return false;

        try
        {
            var document = JsonDocument.Parse(snapshotJson);
            return document.RootElement.TryGetProperty("execution", out var execution) &&
                   execution.TryGetProperty("succeeded", out var succeeded) && succeeded.GetBoolean() &&
                   execution.TryGetProperty("bookingId", out var value) && value.TryGetGuid(out bookingId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string AddExecutionAudit(
        string? snapshotJson,
        bool succeeded,
        string? error,
        Guid? bookingId,
        Guid? decidedByUserId)
    {
        JsonObject root;
        if (string.IsNullOrWhiteSpace(snapshotJson))
        {
            root = new JsonObject();
        }
        else
        {
            try
            {
                root = JsonNode.Parse(snapshotJson) as JsonObject ?? new JsonObject();
            }
            catch (JsonException)
            {
                // Preserve malformed proposal evidence; workflow failure fields carry the safe audit result.
                return snapshotJson;
            }
        }

        root["execution"] = new JsonObject
        {
            ["succeeded"] = succeeded,
            ["bookingId"] = bookingId,
            ["decidedByUserId"] = decidedByUserId,
            ["executedAt"] = DateTime.UtcNow,
            ["error"] = error
        };
        return root.ToJsonString(SnapshotOptions);
    }

    private sealed class BookingActionSnapshot
    {
        public string Contract { get; set; } = string.Empty;
        public Guid WorkflowId { get; set; }
        public Guid TripId { get; set; }
        public string Status { get; set; } = string.Empty;
        public List<BookingActionProposal>? Proposals { get; set; }
        public List<BookingActionIssue>? Issues { get; set; }
        public bool RequiresApproval { get; set; }
        public string Summary { get; set; } = string.Empty;
    }
}
