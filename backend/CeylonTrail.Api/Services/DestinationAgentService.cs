using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CeylonTrail.Api.DTOs.Destination;
using CeylonTrail.Api.Interfaces;

namespace CeylonTrail.Api.Services;

public sealed class DestinationAgentService(
    HttpClient httpClient,
    ILogger<DestinationAgentService> logger) : IDestinationAgentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task<DestinationAgentServiceResult> SelectAsync(
        DestinationAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        var allowedAttractionIds = request.CandidateAttractions
            .Select(candidate => candidate.AttractionId)
            .ToHashSet();
        var requirementReferences = request.Requirements
            .Select(requirement => requirement.Reference)
            .ToHashSet(StringComparer.Ordinal);

        if (allowedAttractionIds.Count != request.CandidateAttractions.Count)
        {
            return Failure("Destination request contains duplicate candidate attraction IDs.");
        }

        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                "destination/select", request, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Destination Agent returned HTTP {StatusCode} for workflow {WorkflowId}.", (int)response.StatusCode, request.WorkflowId);
                return new DestinationAgentServiceResult(
                    Error: "Destination Agent rejected the selection request.",
                    ServiceUnavailable: (int)response.StatusCode >= 500 || (int)response.StatusCode == 429);
            }

            var result = await response.Content.ReadFromJsonAsync<DestinationAgentResponse>(JsonOptions, cancellationToken);
            if (result is null)
            {
                return Failure("Destination Agent returned an empty response.", true);
            }

            var validationError = ValidateResponse(result, request.WorkflowId, allowedAttractionIds, requirementReferences);
            return validationError is null
                ? new DestinationAgentServiceResult(Value: result)
                : Failure(validationError);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Destination Agent timed out for workflow {WorkflowId}.", request.WorkflowId);
            return Failure("Destination Agent request timed out.", true);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Destination Agent was unavailable for workflow {WorkflowId}.", request.WorkflowId);
            return Failure("Destination Agent was unavailable.", true);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Destination Agent returned malformed JSON for workflow {WorkflowId}.", request.WorkflowId);
            return Failure("Destination Agent returned malformed JSON.", true);
        }
    }

    private static string? ValidateResponse(
        DestinationAgentResponse response,
        Guid workflowId,
        IReadOnlySet<Guid> allowedAttractionIds,
        IReadOnlySet<string> requirementReferences)
    {
        if (response.WorkflowId != workflowId)
        {
            return "Destination Agent workflow correlation did not match the request.";
        }

        var selectedIds = new HashSet<Guid>();
        foreach (var selection in response.Selections)
        {
            if (!requirementReferences.Contains(selection.RequirementReference))
            {
                return "Destination Agent returned an unknown requirement reference.";
            }

            if (!allowedAttractionIds.Contains(selection.AttractionId))
            {
                return "Destination Agent returned an attraction outside the approved candidate set.";
            }

            if (!selectedIds.Add(selection.AttractionId))
            {
                return "Destination Agent returned duplicate attraction selections.";
            }

            if (selection.FitReasons.Count == 0 || string.IsNullOrWhiteSpace(selection.Explanation))
            {
                return "Destination Agent returned an incomplete selection.";
            }
        }

        return response.Status is "Selected" or "NoMatch"
            ? null
            : "Destination Agent returned an unsupported status.";
    }

    private static DestinationAgentServiceResult Failure(string error, bool unavailable = false) =>
        new(Error: error, ServiceUnavailable: unavailable);
}
