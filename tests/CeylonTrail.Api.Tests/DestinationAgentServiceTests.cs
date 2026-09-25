using System.Net;
using System.Text;
using System.Text.Json;
using CeylonTrail.Api.DTOs.Destination;
using CeylonTrail.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class DestinationAgentServiceTests
{
    [Fact]
    public async Task ValidResponseIsAccepted()
    {
        var request = CreateRequest();
        var service = CreateService(_ => JsonResponse(CreateResponse(request)));

        var result = await service.SelectAsync(request);

        Assert.NotNull(result.Value);
        Assert.Equal(request.WorkflowId, result.Value!.WorkflowId);
    }

    [Fact]
    public async Task WorkflowIdMismatchIsRejected()
    {
        var request = CreateRequest();
        var response = CreateResponse(request) with { WorkflowId = Guid.NewGuid() };

        var result = await CreateService(_ => JsonResponse(response)).SelectAsync(request);

        Assert.Null(result.Value);
        Assert.Contains("correlation", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownAttractionIdIsRejected()
    {
        var request = CreateRequest();
        var response = CreateResponse(request) with
        {
            Selections = [new("slot-1", Guid.NewGuid(), ["interest_match"], "Unknown")]
        };

        var result = await CreateService(_ => JsonResponse(response)).SelectAsync(request);

        Assert.Null(result.Value);
        Assert.Contains("approved candidate", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateSelectionIsRejected()
    {
        var request = CreateRequest();
        var candidate = request.CandidateAttractions[0].AttractionId;
        var response = CreateResponse(request) with
        {
            Selections =
            [
                new("slot-1", candidate, ["interest_match"], "First"),
                new("slot-1", candidate, ["interest_match"], "Duplicate")
            ]
        };

        var result = await CreateService(_ => JsonResponse(response)).SelectAsync(request);

        Assert.Null(result.Value);
        Assert.Contains("duplicate", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MalformedResponseIsHandledSafely()
    {
        var request = CreateRequest();
        var result = await CreateService(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{not-json", Encoding.UTF8, "application/json")
        }).SelectAsync(request);

        Assert.Null(result.Value);
        Assert.Contains("malformed", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.ServiceUnavailable);
    }

    [Fact]
    public async Task TimeoutIsHandledSafely()
    {
        var request = CreateRequest();
        var result = await CreateService(_ => throw new OperationCanceledException()).SelectAsync(request);

        Assert.Null(result.Value);
        Assert.True(result.ServiceUnavailable);
        Assert.Contains("timed out", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DuplicateOriginalCandidateAllowListIsRejectedBeforeCall()
    {
        var request = CreateRequest();
        request = request with { CandidateAttractions = [request.CandidateAttractions[0], request.CandidateAttractions[0]] };
        var called = false;

        var result = await CreateService(_ =>
        {
            called = true;
            return JsonResponse(CreateResponse(request));
        }).SelectAsync(request);

        Assert.False(called);
        Assert.Contains("duplicate candidate", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ServiceDoesNotPersistItineraryOrAttractionChanges()
    {
        var request = CreateRequest();
        var service = CreateService(_ => JsonResponse(CreateResponse(request)));

        var result = await service.SelectAsync(request);

        Assert.True(result.Value is not null);
        Assert.DoesNotContain("DbContext", typeof(DestinationAgentService).GetConstructors()[0].GetParameters().Select(parameter => parameter.ParameterType.Name));
    }

    private static DestinationAgentService CreateService(Func<HttpRequestMessage, HttpResponseMessage> send) =>
        new(new HttpClient(new RecordingHandler(send)) { BaseAddress = new Uri("http://localhost:8003/") }, NullLogger<DestinationAgentService>.Instance);

    private static DestinationAgentRequest CreateRequest()
    {
        var candidateId = Guid.NewGuid();
        return new(
            Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), 1, 5000m,
            ["Culture"], ["Kandy"],
            [new("slot-1", "Culture", "Kandy", ["Culture"], null)],
            [new(candidateId, "Temple", "Culture", "Kandy", "Trusted summary", 2000m, null)]);
    }

    private static DestinationAgentResponse CreateResponse(DestinationAgentRequest request) =>
        new(request.WorkflowId,
            [new("slot-1", request.CandidateAttractions[0].AttractionId, ["interest_match"], "Matched approved candidate.")],
            [], "Selected", "Selected one candidate.");

    private static HttpResponseMessage JsonResponse(DestinationAgentResponse response) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(send(request));
    }
}
