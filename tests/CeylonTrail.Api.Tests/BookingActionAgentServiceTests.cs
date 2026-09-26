using System.Net;
using System.Text;
using System.Text.Json;
using CeylonTrail.Api.Data;
using CeylonTrail.Api.DTOs.BookingAction;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using CeylonTrail.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class BookingActionAgentServiceTests
{
    [Fact]
    public async Task RequestUsesAuthoritativeSnapshotAndValidResponseIsAccepted()
    {
        var request = CreateRequest();
        var snapshot = CreateSnapshot();
        BookingActionExecutionRequest? sent = null;
        var response = CreateResponse(request, snapshot);
        var service = CreateService(snapshot, message =>
        {
            sent = JsonSerializer.Deserialize<BookingActionExecutionRequest>(message.Content!.ReadAsStringAsync().Result, JsonOptions());
            return JsonResponse(response);
        });

        var result = await service.PrepareAsync(request);

        Assert.True(result.Succeeded);
        Assert.NotNull(sent);
        Assert.Equal(snapshot[0].PricePerPerson, sent!.TrustedAvailabilitySlots[0].PricePerPerson);
        Assert.Equal(request.SelectedAttractionIds, sent.SelectedAttractionIds);
    }

    [Fact]
    public async Task UnknownSlotIsRejected()
    {
        var request = CreateRequest();
        var snapshot = CreateSnapshot();
        var response = CreateResponse(request, snapshot) with
        {
            Proposals = [new(snapshot[0].AttractionId, Guid.NewGuid(), request.GuestCount, snapshot[0].PricePerPerson, snapshot[0].PricePerPerson * request.GuestCount, snapshot[0].StartTime, snapshot[0].EndTime, "test")]
        };

        var result = await CreateService(snapshot, _ => JsonResponse(response)).PrepareAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("unknown availability", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AttractionMismatchIsRejected()
    {
        var request = CreateRequest();
        var snapshot = CreateSnapshot();
        var response = CreateResponse(request, snapshot) with
        {
            Proposals = [new(Guid.NewGuid(), snapshot[0].AvailabilitySlotId, request.GuestCount, snapshot[0].PricePerPerson, snapshot[0].PricePerPerson * request.GuestCount, snapshot[0].StartTime, snapshot[0].EndTime, "test")]
        };

        var result = await CreateService(snapshot, _ => JsonResponse(response)).PrepareAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("attraction mismatch", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PriceTamperingIsRejected()
    {
        var request = CreateRequest();
        var snapshot = CreateSnapshot();
        var response = CreateResponse(request, snapshot) with
        {
            Proposals = [new(snapshot[0].AttractionId, snapshot[0].AvailabilitySlotId, request.GuestCount, 0.01m, 0.02m, snapshot[0].StartTime, snapshot[0].EndTime, "test")]
        };

        var result = await CreateService(snapshot, _ => JsonResponse(response)).PrepareAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("tampered unit price", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TotalPriceTamperingIsRejected()
    {
        var request = CreateRequest();
        var snapshot = CreateSnapshot();
        var response = CreateResponse(request, snapshot) with
        {
            Proposals = [new(snapshot[0].AttractionId, snapshot[0].AvailabilitySlotId, request.GuestCount, snapshot[0].PricePerPerson, 999m, snapshot[0].StartTime, snapshot[0].EndTime, "test")]
        };

        var result = await CreateService(snapshot, _ => JsonResponse(response)).PrepareAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("tampered total", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuestCountMismatchIsRejected()
    {
        var request = CreateRequest();
        var snapshot = CreateSnapshot();
        var response = CreateResponse(request, snapshot) with
        {
            Proposals = [new(snapshot[0].AttractionId, snapshot[0].AvailabilitySlotId, 99, snapshot[0].PricePerPerson, snapshot[0].PricePerPerson * 99, snapshot[0].StartTime, snapshot[0].EndTime, "test")]
        };

        var result = await CreateService(snapshot, _ => JsonResponse(response)).PrepareAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("guest-count", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateProposalIsRejected()
    {
        var request = CreateRequest();
        var snapshot = CreateSnapshot();
        var proposal = CreateResponse(request, snapshot).Proposals[0];
        var response = CreateResponse(request, snapshot) with { Proposals = [proposal, proposal] };

        var result = await CreateService(snapshot, _ => JsonResponse(response)).PrepareAsync(request);

        Assert.False(result.Succeeded);
        Assert.Contains("duplicate", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MalformedJsonFailsClosed()
    {
        var request = CreateRequest();
        var result = await CreateService(CreateSnapshot(), _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-json", Encoding.UTF8, "application/json")
        }).PrepareAsync(request);

        Assert.False(result.Succeeded);
        Assert.True(result.ServiceUnavailable);
    }

    [Fact]
    public async Task NonSuccessHttpResponseFailsClosed()
    {
        var request = CreateRequest();
        var result = await CreateService(CreateSnapshot(), _ => new HttpResponseMessage(HttpStatusCode.BadGateway))
            .PrepareAsync(request);

        Assert.False(result.Succeeded);
        Assert.True(result.ServiceUnavailable);
    }

    [Fact]
    public async Task TimeoutFailsClosed()
    {
        var request = CreateRequest();
        var result = await CreateService(CreateSnapshot(), _ => throw new OperationCanceledException()).PrepareAsync(request);

        Assert.False(result.Succeeded);
        Assert.True(result.ServiceUnavailable);
        Assert.Contains("timed out", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ServiceHasNoBookingMutationDependency()
    {
        var constructorTypes = typeof(BookingActionAgentService).GetConstructors()[0]
            .GetParameters().Select(parameter => parameter.ParameterType.Name).ToArray();

        Assert.DoesNotContain(nameof(IBookingService), constructorTypes);
        Assert.DoesNotContain(nameof(ApplicationDbContext), constructorTypes);
    }

    [Fact]
    public async Task SnapshotServiceReturnsOnlyFutureEligibleApprovedSlots()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var category = new Category { Id = Guid.NewGuid(), Name = "Culture" };
        var attraction = new Attraction
        {
            Id = Guid.NewGuid(), ProviderId = Guid.NewGuid(), CategoryId = category.Id, Category = category,
            Name = "Temple", Description = "Trusted", District = "Kandy", Address = "Kandy", Price = 100,
            Status = "Approved", IsActive = true
        };
        var eligible = new AvailabilitySlot
        {
            Id = Guid.NewGuid(), AttractionId = attraction.Id, Attraction = attraction,
            StartTime = DateTime.UtcNow.AddDays(1), EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
            MaxCapacity = 10, BookedCapacity = 2, PricePerPerson = 25
        };
        var expired = new AvailabilitySlot
        {
            Id = Guid.NewGuid(), AttractionId = attraction.Id, Attraction = attraction,
            StartTime = DateTime.UtcNow.AddHours(-3), EndTime = DateTime.UtcNow.AddHours(-1),
            MaxCapacity = 10, BookedCapacity = 2, PricePerPerson = 25
        };
        db.Attractions.Add(attraction);
        db.AvailabilitySlots.AddRange(eligible, expired);
        await db.SaveChangesAsync();

        var result = await new BookingAvailabilitySnapshotService(db).GetTrustedFutureSlotsAsync([attraction.Id]);

        var slot = Assert.Single(result);
        Assert.Equal(eligible.Id, slot.AvailabilitySlotId);
        Assert.Equal(8, slot.AvailableCapacity);
        Assert.Equal(25m, slot.PricePerPerson);
    }

    private static BookingActionAgentService CreateService(
        IReadOnlyList<TrustedBookingAvailabilitySlot> snapshot,
        Func<HttpRequestMessage, HttpResponseMessage> send) =>
        new(new FakeSnapshotService(snapshot), new HttpClient(new RecordingHandler(send)) { BaseAddress = new Uri("http://localhost:8004/") }, NullLogger<BookingActionAgentService>.Instance);

    private static BookingActionAgentRequest CreateRequest() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 2, 100m, [Guid.Parse("11111111-1111-1111-1111-111111111111")], new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5));

    private static TrustedBookingAvailabilitySlot[] CreateSnapshot()
    {
        var attractionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        return [new(Guid.NewGuid(), attractionId, "Temple", new DateTime(2026, 10, 2, 9, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 2, 11, 0, 0, DateTimeKind.Utc), 25m, 10, 0, 10, true, true)];
    }

    private static BookingActionAgentResponse CreateResponse(BookingActionAgentRequest request, IReadOnlyList<TrustedBookingAvailabilitySlot> snapshot) =>
        new(request.WorkflowId, request.TripId, "Prepared",
            [new(snapshot[0].AttractionId, snapshot[0].AvailabilitySlotId, request.GuestCount, snapshot[0].PricePerPerson, snapshot[0].PricePerPerson * request.GuestCount, snapshot[0].StartTime, snapshot[0].EndTime, "Eligible")],
            [], true, "Prepared proposal.");

    private static JsonSerializerOptions JsonOptions() => new(JsonSerializerDefaults.Web);

    private static HttpResponseMessage JsonResponse(BookingActionAgentResponse response) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(response, JsonOptions()), Encoding.UTF8, "application/json")
        };

    private sealed class FakeSnapshotService(IReadOnlyList<TrustedBookingAvailabilitySlot> snapshot) : IBookingAvailabilitySnapshotService
    {
        public Task<IReadOnlyList<TrustedBookingAvailabilitySlot>> GetTrustedFutureSlotsAsync(IReadOnlyCollection<Guid> attractionIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(send(request));
    }
}
