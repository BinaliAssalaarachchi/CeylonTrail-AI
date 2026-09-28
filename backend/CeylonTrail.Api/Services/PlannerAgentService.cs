using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CeylonTrail.Api.DTOs.Planner;
using CeylonTrail.Api.Interfaces;

namespace CeylonTrail.Api.Services;

public sealed class PlannerAgentService(
    HttpClient httpClient,
    ILogger<PlannerAgentService> logger) : IPlannerAgentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<PlannerAgentServiceResult> GenerateAsync(
        PlannerAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation(
                "Sending Planner request for trip {TripId}: dates {StartDate} to {EndDate}, duration {Duration}, " +
                "budget {Budget}, interests {InterestCount}, regions {RegionCount}, preferences {PreferenceCount}, " +
                "maxPreferenceLength {MaxPreferenceLength}, candidates {CandidateCount}.",
                request.TripId,
                request.StartDate,
                request.EndDate,
                request.Duration,
                request.Budget,
                request.Interests.Count,
                request.PreferredRegions.Count,
                request.Preferences.Count,
                request.Preferences.Count == 0 ? 0 : request.Preferences.Max(item => item.Value.Length),
                request.CandidateAttractions.Count);

            using var response = await httpClient.PostAsJsonAsync(
                "planner/generate",
                request,
                JsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
                if (responseBody.Length > 4000)
                    responseBody = responseBody[..4000];

                logger.LogWarning(
                    "Planner Agent returned HTTP {StatusCode} for trip {TripId}. Response: {ResponseBody}",
                    (int)response.StatusCode,
                    request.TripId,
                    responseBody);
                return new PlannerAgentServiceResult(
                    Error: "Planner Agent rejected the itinerary request.",
                    ServiceUnavailable: (int)response.StatusCode >= 500 || (int)response.StatusCode == 429);
            }

            var result = await response.Content.ReadFromJsonAsync<PlannerAgentResponse>(
                JsonOptions,
                cancellationToken);
            return result is null
                ? new PlannerAgentServiceResult(Error: "Planner Agent returned an empty response.", ServiceUnavailable: true)
                : new PlannerAgentServiceResult(Value: result);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Planner Agent timed out for trip {TripId}.", request.TripId);
            return new PlannerAgentServiceResult(Error: "Planner Agent request timed out.", ServiceUnavailable: true);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Planner Agent was unavailable for trip {TripId}.", request.TripId);
            return new PlannerAgentServiceResult(Error: "Planner Agent was unavailable.", ServiceUnavailable: true);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Planner Agent returned malformed JSON for trip {TripId}.", request.TripId);
            return new PlannerAgentServiceResult(Error: "Planner Agent returned malformed JSON.", ServiceUnavailable: true);
        }
    }
}
