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
            using var response = await httpClient.PostAsJsonAsync(
                "planner/generate",
                request,
                JsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Planner Agent returned HTTP {StatusCode} for trip {TripId}.",
                    (int)response.StatusCode,
                    request.TripId);
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
