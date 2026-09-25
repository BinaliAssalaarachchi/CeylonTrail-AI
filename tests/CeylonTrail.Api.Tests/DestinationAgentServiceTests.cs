using System.Net;
using System.Net.Http.Json;
using System.Text;
using CeylonTrail.Api.DTOs.Attractions;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class DestinationAgentServiceTests
{
    [Fact]
    public async Task RecommendAsyncSendsTrustedAttractionsAndReturnsAgentResponse()
    {
        var attraction = CreateAttraction();
        var request = new DestinationRecommendationRequest { District = "Kandy", Interests = ["Culture"], MaxBudget = 5000m, Date = new DateOnly(2026, 10, 1), Limit = 3 };
        HttpRequestMessage? capturedRequest = null;
        var expected = new DestinationAgentResponse(
            [new(attraction.Id, attraction.Name, attraction.District, attraction.CategoryId, attraction.Category.Name, attraction.Price, [], [], ["culture"], 90)],
            "Success", "Recommendations generated.");

        var result = await CreateService(new StubAttractionService(attraction), requestMessage =>
        {
            capturedRequest = requestMessage;
            return JsonResponse(expected);
        }).RecommendAsync(request);

        Assert.NotNull(result.Value);
        Assert.Equal("Success", result.Value!.Status);
        var payload = await capturedRequest!.Content!.ReadFromJsonAsync<DestinationAgentRequest>();
        Assert.NotNull(payload);
        Assert.Single(payload!.TrustedAttractions);
        Assert.Equal(attraction.Id, payload.TrustedAttractions[0].AttractionId);
        Assert.Equal(request.District, payload.Request.District);
    }

    [Fact]
    public async Task RecommendAsyncReturnsUnavailableWhenCategoriesCannotBeLoaded()
    {
        var service = CreateService(new StubAttractionService
        {
            CategoriesResult = ServiceResult<IReadOnlyList<CategoryResponse>>.Failure("Unavailable", ServiceErrorCode.Validation)
        }, _ => throw new InvalidOperationException("The agent should not be called."));

        var result = await service.RecommendAsync(new DestinationRecommendationRequest());

        Assert.Null(result.Value);
        Assert.True(result.ServiceUnavailable);
        Assert.Contains("categories", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RecommendAsyncHandlesMalformedAgentResponse()
    {
        var service = CreateService(new StubAttractionService(CreateAttraction()), _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-json", Encoding.UTF8, "application/json")
        });

        var result = await service.RecommendAsync(new DestinationRecommendationRequest());

        Assert.Null(result.Value);
        Assert.True(result.ServiceUnavailable);
        Assert.Contains("malformed", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private static DestinationAgentService CreateService(IAttractionService attractionService, Func<HttpRequestMessage, HttpResponseMessage> send) =>
        new(attractionService, new HttpClient(new RecordingHandler(send)) { BaseAddress = new Uri("http://localhost:8003/") }, NullLogger<DestinationAgentService>.Instance);

    private static AttractionResponse CreateAttraction()
    {
        var categoryId = Guid.NewGuid();
        return new(Guid.NewGuid(), Guid.NewGuid(), categoryId, "Temple of the Tooth", "A cultural landmark.", "Kandy", "Kandy city", 7.29m, 80.64m, 2000m, "Approved", true, DateTime.UtcNow.AddDays(-2), DateTime.UtcNow, new CategoryResponse(categoryId, "Culture", "Cultural attractions."), [], [], [], false);
    }

    private static HttpResponseMessage JsonResponse(DestinationAgentResponse response) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(response) };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(send(request));
    }

    private sealed class StubAttractionService : IAttractionService
    {
        private readonly AttractionResponse? attraction;

        public StubAttractionService(AttractionResponse? attraction = null) => this.attraction = attraction;

        public ServiceResult<IReadOnlyList<CategoryResponse>> CategoriesResult { get; init; } = ServiceResult<IReadOnlyList<CategoryResponse>>.Success([new(Guid.NewGuid(), "Culture", "Cultural attractions.")]);
        public Task<ServiceResult<AttractionResponse>> CreateAsync(CreateAttractionRequest request, Guid providerId, CancellationToken cancellationToken = default) => Unsupported<AttractionResponse>();
        public Task<ServiceResult<AttractionSearchResponse>> SearchAsync(AttractionSearchRequest request, Guid? viewerId = null, CancellationToken cancellationToken = default) => Task.FromResult(ServiceResult<AttractionSearchResponse>.Success(new([attraction!], 1, 1, 100, 1)));
        public Task<ServiceResult<AttractionResponse>> GetByIdAsync(Guid attractionId, Guid? viewerId = null, CancellationToken cancellationToken = default) => Unsupported<AttractionResponse>();
        public Task<ServiceResult<AttractionSearchResponse>> GetMineAsync(AttractionSearchRequest request, Guid providerId, CancellationToken cancellationToken = default) => Unsupported<AttractionSearchResponse>();
        public Task<ServiceResult<AttractionSearchResponse>> GetPendingAsync(AttractionSearchRequest request, CancellationToken cancellationToken = default) => Unsupported<AttractionSearchResponse>();
        public Task<ServiceResult<IReadOnlyList<CategoryResponse>>> GetCategoriesAsync(CancellationToken cancellationToken = default) => Task.FromResult(CategoriesResult);
        public Task<ServiceResult<AttractionSearchResponse>> GetFavoritesAsync(AttractionSearchRequest request, Guid touristId, CancellationToken cancellationToken = default) => Unsupported<AttractionSearchResponse>();
        public Task<ServiceResult<AttractionResponse>> ApproveAsync(Guid attractionId, CancellationToken cancellationToken = default) => Unsupported<AttractionResponse>();
        public Task<ServiceResult<AttractionResponse>> UpdateAsync(Guid attractionId, UpdateAttractionRequest request, Guid actorId, CancellationToken cancellationToken = default) => Unsupported<AttractionResponse>();
        public Task<ServiceResult<bool>> DeleteAsync(Guid attractionId, Guid actorId, CancellationToken cancellationToken = default) => Unsupported<bool>();
        public Task<ServiceResult<AttractionResponse>> ActivateAsync(Guid attractionId, Guid actorId, CancellationToken cancellationToken = default) => Unsupported<AttractionResponse>();
        public Task<ServiceResult<AttractionScheduleResponse>> AddScheduleAsync(Guid attractionId, CreateScheduleRequest request, Guid actorId, CancellationToken cancellationToken = default) => Unsupported<AttractionScheduleResponse>();
        public Task<ServiceResult<AttractionScheduleResponse>> UpdateScheduleAsync(Guid attractionId, Guid scheduleId, CreateScheduleRequest request, Guid actorId, CancellationToken cancellationToken = default) => Unsupported<AttractionScheduleResponse>();
        public Task<ServiceResult<bool>> DeleteScheduleAsync(Guid attractionId, Guid scheduleId, Guid actorId, CancellationToken cancellationToken = default) => Unsupported<bool>();
        public Task<ServiceResult<ExperienceSlotResponse>> AddSlotAsync(Guid attractionId, CreateExperienceSlotRequest request, Guid actorId, CancellationToken cancellationToken = default) => Unsupported<ExperienceSlotResponse>();
        public Task<ServiceResult<ExperienceSlotResponse>> UpdateSlotAsync(Guid attractionId, Guid slotId, CreateExperienceSlotRequest request, Guid actorId, CancellationToken cancellationToken = default) => Unsupported<ExperienceSlotResponse>();
        public Task<ServiceResult<bool>> DeleteSlotAsync(Guid attractionId, Guid slotId, Guid actorId, CancellationToken cancellationToken = default) => Unsupported<bool>();
        public Task<ServiceResult<AvailabilityResponse>> GetAvailabilityAsync(Guid attractionId, DateOnly? date, CancellationToken cancellationToken = default) => Unsupported<AvailabilityResponse>();
        public Task<ServiceResult<FavoriteResponse>> AddFavoriteAsync(Guid attractionId, Guid touristId, CancellationToken cancellationToken = default) => Unsupported<FavoriteResponse>();
        public Task<ServiceResult<bool>> RemoveFavoriteAsync(Guid attractionId, Guid touristId, CancellationToken cancellationToken = default) => Unsupported<bool>();
        public Task<ServiceResult<AttractionImageResponse>> AddImageAsync(Guid attractionId, AttractionImageRequest request, Guid actorId, CancellationToken cancellationToken = default) => Unsupported<AttractionImageResponse>();
        public Task<ServiceResult<bool>> RemoveImageAsync(Guid attractionId, Guid imageId, Guid actorId, CancellationToken cancellationToken = default) => Unsupported<bool>();
        public Task<ServiceResult<AttractionImageResponse>> SetPrimaryImageAsync(Guid attractionId, Guid imageId, Guid actorId, CancellationToken cancellationToken = default) => Unsupported<AttractionImageResponse>();
        private static Task<ServiceResult<T>> Unsupported<T>() => Task.FromException<ServiceResult<T>>(new NotSupportedException());
    }
}
