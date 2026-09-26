using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CeylonTrail.Api.DTOs.BookingAction;
using CeylonTrail.Api.Interfaces;

namespace CeylonTrail.Api.Services;

public sealed class BookingActionAgentService(
    IBookingAvailabilitySnapshotService availabilitySnapshotService,
    HttpClient httpClient,
    ILogger<BookingActionAgentService> logger) : IBookingActionAgentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<BookingActionAgentServiceResult> PrepareAsync(
        BookingActionAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.WorkflowId == Guid.Empty || request.TripId == Guid.Empty || request.GuestCount <= 0)
        {
            return Failure("Workflow, trip, and positive guest count are required.");
        }

        if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate < request.StartDate)
        {
            return Failure("End date must be on or after start date.");
        }

        if (request.SelectedAttractionIds.Any(id => id == Guid.Empty) ||
            request.SelectedAttractionIds.Count != request.SelectedAttractionIds.Distinct().Count())
        {
            return Failure("Selected attraction IDs must be non-empty and unique.");
        }

        var snapshot = await availabilitySnapshotService.GetTrustedFutureSlotsAsync(
            request.SelectedAttractionIds, cancellationToken);
        var payload = new BookingActionExecutionRequest(
            request.WorkflowId,
            request.TripId,
            request.GuestCount,
            request.RemainingBudget,
            request.SelectedAttractionIds,
            request.StartDate,
            request.EndDate,
            snapshot);

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "booking/prepare", payload, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Booking Action Agent returned HTTP {StatusCode} for workflow {WorkflowId}.", (int)response.StatusCode, request.WorkflowId);
                return Failure("Booking Action Agent rejected the proposal request.", (int)response.StatusCode >= 500 || (int)response.StatusCode == 429);
            }

            var result = await response.Content.ReadFromJsonAsync<BookingActionAgentResponse>(JsonOptions, cancellationToken);
            if (result is null)
            {
                return Failure("Booking Action Agent returned an empty response.", true);
            }

            var validationError = ValidateResponse(result, request, snapshot);
            return validationError is null
                ? new BookingActionAgentServiceResult(Value: result)
                : Failure(validationError);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Booking Action Agent timed out for workflow {WorkflowId}.", request.WorkflowId);
            return Failure("Booking Action Agent request timed out.", true);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Booking Action Agent was unavailable for workflow {WorkflowId}.", request.WorkflowId);
            return Failure("Booking Action Agent was unavailable.", true);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Booking Action Agent returned malformed JSON for workflow {WorkflowId}.", request.WorkflowId);
            return Failure("Booking Action Agent returned malformed JSON.", true);
        }
    }

    private static string? ValidateResponse(
        BookingActionAgentResponse response,
        BookingActionAgentRequest request,
        IReadOnlyList<TrustedBookingAvailabilitySlot> snapshot)
    {
        if (response.WorkflowId != request.WorkflowId || response.TripId != request.TripId)
        {
            return "Booking Action Agent workflow or trip correlation did not match the request.";
        }

        if (response.Proposals.Count > 100 || response.Issues.Count > 100)
        {
            return "Booking Action Agent response exceeds bounded limits.";
        }

        var bySlot = snapshot.ToDictionary(slot => slot.AvailabilitySlotId);
        var selectedAttractions = request.SelectedAttractionIds.ToHashSet();
        var proposalIds = new HashSet<Guid>();
        var total = 0m;
        foreach (var proposal in response.Proposals)
        {
            if (!bySlot.TryGetValue(proposal.AvailabilitySlotId, out var slot))
            {
                return "Booking Action Agent returned an unknown availability slot.";
            }

            if (!proposalIds.Add(proposal.AvailabilitySlotId))
            {
                return "Booking Action Agent returned duplicate availability slots.";
            }

            if (!selectedAttractions.Contains(proposal.AttractionId) || proposal.AttractionId != slot.AttractionId)
            {
                return "Booking Action Agent returned an attraction mismatch.";
            }

            if (proposal.GuestCount != request.GuestCount)
            {
                return "Booking Action Agent returned a guest-count mismatch.";
            }

            if (proposal.UnitPrice != slot.PricePerPerson)
            {
                return "Booking Action Agent returned a tampered unit price.";
            }

            if (proposal.TotalPrice != slot.PricePerPerson * request.GuestCount)
            {
                return "Booking Action Agent returned a tampered total price.";
            }

            if (proposal.StartTime != slot.StartTime || proposal.EndTime != slot.EndTime)
            {
                return "Booking Action Agent returned times that differ from the authoritative slot.";
            }

            if (!slot.IsActive || !slot.IsApproved || slot.AvailableCapacity < request.GuestCount)
            {
                return "Booking Action Agent returned a proposal that is no longer logically feasible.";
            }

            if (string.IsNullOrWhiteSpace(proposal.Reason) || proposal.Reason.Length > 300)
            {
                return "Booking Action Agent returned an invalid proposal reason.";
            }

            total += proposal.TotalPrice;
        }

        if (request.RemainingBudget.HasValue && total > request.RemainingBudget.Value)
        {
            return "Booking Action Agent proposals exceed the remaining budget.";
        }

        foreach (var issue in response.Issues)
        {
            if (string.IsNullOrWhiteSpace(issue.Code) || issue.Code.Length > 40 ||
                string.IsNullOrWhiteSpace(issue.Message) || issue.Message.Length > 300)
            {
                return "Booking Action Agent returned an invalid issue.";
            }

            if (issue.AvailabilitySlotId.HasValue && !bySlot.ContainsKey(issue.AvailabilitySlotId.Value))
            {
                return "Booking Action Agent issue references an unknown availability slot.";
            }
        }

        if (response.Status == "Prepared" && (response.Proposals.Count == 0 || !response.RequiresApproval))
        {
            return "Prepared output must contain proposals and require approval.";
        }

        if (response.Status == "NoEligibleProposal" && (response.Proposals.Count != 0 || response.RequiresApproval))
        {
            return "NoEligibleProposal output cannot contain proposals or request approval.";
        }

        if (response.Status is not ("Prepared" or "NoEligibleProposal") ||
            string.IsNullOrWhiteSpace(response.Summary) || response.Summary.Length > 500)
        {
            return "Booking Action Agent returned an invalid status or summary.";
        }

        return null;
    }

    private static BookingActionAgentServiceResult Failure(string error, bool unavailable = false) =>
        new(Error: error, ServiceUnavailable: unavailable);
}
