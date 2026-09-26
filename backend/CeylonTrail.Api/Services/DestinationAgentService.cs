using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CeylonTrail.Api.Configuration;
using CeylonTrail.Api.DTOs.Attractions;
using CeylonTrail.Api.Interfaces;
using Microsoft.Extensions.Options;

namespace CeylonTrail.Api.Services;

public sealed class DestinationAgentService(
    IAttractionService attractionService,
    HttpClient httpClient,
    ILogger<DestinationAgentService> logger) : IDestinationAgentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<DestinationAgentServiceResult> RecommendAsync(
        DestinationRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var categoriesResult = await attractionService.GetCategoriesAsync(cancellationToken);
            if (!categoriesResult.Succeeded)
                return new(Error: "Attraction categories are unavailable.", ServiceUnavailable: true);

            var interestNames = request.Interests
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var interestCategoryIds = categoriesResult.Value!
                .Where(category => interestNames.Contains(category.Name))
                .Select(category => category.Id)
                .ToList();
            var categoryIds = request.CategoryIds.Count > 0
                ? request.CategoryIds
                : interestCategoryIds;

            var searches = categoryIds.Count == 0
                ? new[] { (Guid?)null }
                : categoryIds.Select(id => (Guid?)id).ToArray();
            var trusted = new Dictionary<Guid, DestinationTrustedAttraction>();
            foreach (var categoryId in searches)
            {
                var search = await attractionService.SearchAsync(new AttractionSearchRequest
                {
                    District = request.District,
                    CategoryId = categoryId,
                    MaxPrice = request.MaxBudget,
                    Date = request.Date,
                    Page = 1,
                    PageSize = 100,
                    Sort = "name_asc"
                }, cancellationToken: cancellationToken);
                if (!search.Succeeded)
                    return new(Error: "Attraction search failed.", ServiceUnavailable: true);

                foreach (var item in search.Value!.Items)
                    trusted[item.Id] = ToTrusted(item);
            }

            var payload = new DestinationAgentRequest(request, trusted.Values.ToList());
            using var response = await httpClient.PostAsJsonAsync("destination/recommend", payload, JsonOptions, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(Error: "Destination Agent rejected the recommendation request.", ServiceUnavailable: (int)response.StatusCode >= 500 || (int)response.StatusCode == 429);

            var result = await response.Content.ReadFromJsonAsync<DestinationAgentResponse>(JsonOptions, cancellationToken);
            if (result is null)
                return new(Error: "Destination Agent returned an empty response.", ServiceUnavailable: true);

            var validationError = ValidateResponse(result, trusted, request);
            return validationError is null
                ? new(Value: result)
                : new(Error: validationError);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Destination Agent timed out.");
            return new(Error: "Destination Agent request timed out.", ServiceUnavailable: true);
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Destination Agent was unavailable.");
            return new(Error: "Destination Agent was unavailable.", ServiceUnavailable: true);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Destination Agent returned malformed JSON.");
            return new(Error: "Destination Agent returned malformed JSON.", ServiceUnavailable: true);
        }
    }

    private static DestinationTrustedAttraction ToTrusted(AttractionResponse item) => new(
        item.Id,
        item.Name,
        item.District,
        item.CategoryId,
        item.Category.Name,
        item.Price,
        item.Status,
        item.IsActive,
        item.Schedules.Select(schedule => new DestinationTrustedSchedule(schedule.DayOfWeek, schedule.OpeningTime, schedule.ClosingTime, schedule.IsClosed)).ToList(),
        item.ExperienceSlots.Select(slot => new DestinationTrustedSlot(slot.Date, slot.StartTime, slot.EndTime, slot.Capacity, slot.AvailableCapacity)).ToList());

    private static string? ValidateResponse(
        DestinationAgentResponse response,
        IReadOnlyDictionary<Guid, DestinationTrustedAttraction> trusted,
        DestinationRecommendationRequest request)
    {
        if (response.Candidates.Count > 50 || response.Status is not ("Success" or "NoResults"))
            return "Destination Agent returned an invalid bounded response.";

        var seen = new HashSet<Guid>();
        foreach (var candidate in response.Candidates)
        {
            if (!trusted.TryGetValue(candidate.AttractionId, out var source))
                return "Destination Agent returned an unknown attraction.";
            if (!seen.Add(candidate.AttractionId))
                return "Destination Agent returned duplicate attractions.";
            if (!source.IsActive || !string.Equals(source.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                return "Destination Agent returned a non-public attraction.";
            if (candidate.Name != source.Name || candidate.District != source.District ||
                candidate.CategoryId != source.CategoryId || candidate.Category != source.Category ||
                candidate.Price != source.Price)
                return "Destination Agent changed authoritative attraction facts.";

            var expectedAvailability = source.ExperienceSlots
                .Where(slot => !request.Date.HasValue || slot.Date == request.Date.Value)
                .ToList();
            if (!candidate.Availability.SequenceEqual(expectedAvailability) ||
                !candidate.OpeningHours.SequenceEqual(source.Schedules))
                return "Destination Agent changed authoritative attraction availability.";
        }

        if (response.Status == "NoResults" && response.Candidates.Count > 0)
            return "Destination Agent returned candidates with a NoResults status.";
        if (response.Status == "Success" && response.Candidates.Count == 0)
            return "Destination Agent returned Success without candidates.";
        return null;
    }
}
