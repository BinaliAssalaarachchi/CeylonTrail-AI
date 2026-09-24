using System.Security.Claims;
using CeylonTrail.Api.Controllers;
using CeylonTrail.Api.DTOs.ApprovalRequests;
using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.DTOs.TravelIntelligence;
using CeylonTrail.Api.Interfaces;
using CeylonTrail.Api.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace CeylonTrail.Api.Tests;

public sealed class ItineraryValidationsControllerTests
{
    [Fact]
    public async Task AnalyzeWithTravelIntelligence_UsesPersistedValidationAndReturnsRecommendation()
    {
        var userId = Guid.NewGuid();
        var validation = new ItineraryValidationResponse
        {
            Id = Guid.NewGuid(),
            OverallStatus = ValidationOverallStatus.Invalid,
            RiskLevel = ValidationRiskLevel.Critical,
            IsFeasible = false,
            TotalIssueCount = 1,
            BlockingIssueCount = 1
        };
        var validationService = new RecordingValidationService(validation);
        var intelligenceService = new RecordingIntelligenceService();
        var controller = CreateController(validationService, intelligenceService, userId);

        var result = await controller.AnalyzeWithTravelIntelligence(
            validation.Id,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(validation, intelligenceService.ReceivedValidation);
        Assert.Equal(userId, validationService.ReceivedUserId);
        Assert.IsType<TravelIntelligenceResponse>(ok.Value);
    }

    [Fact]
    public async Task AnalyzeWithTravelIntelligence_WhenValidationIsNotOwned_ReturnsNotFound()
    {
        var userId = Guid.NewGuid();
        var validationService = new RecordingValidationService(null);
        var controller = CreateController(
            validationService,
            new RecordingIntelligenceService(),
            userId);

        var result = await controller.AnalyzeWithTravelIntelligence(
            Guid.NewGuid(),
            CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task AnalyzeWithTravelIntelligence_WhenPersistenceFails_ReturnsServerErrorWithoutRetryingAnalysis()
    {
        var userId = Guid.NewGuid();
        var validation = new ItineraryValidationResponse
        {
            Id = Guid.NewGuid(),
            OverallStatus = ValidationOverallStatus.Valid,
            RiskLevel = ValidationRiskLevel.Low,
            IsFeasible = true
        };
        var intelligenceService = new RecordingIntelligenceService();
        var controller = CreateController(
            new RecordingValidationService(validation),
            intelligenceService,
            userId,
            new RecordingPersistenceService(
                TravelIntelligenceExecutionPersistenceResult.Failure("persistence failed")));

        var result = await controller.AnalyzeWithTravelIntelligence(
            validation.Id,
            CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status500InternalServerError, error.StatusCode);
        Assert.Equal(1, intelligenceService.CallCount);
    }

    private static ItineraryValidationsController CreateController(
        IItineraryValidationService validationService,
        ITravelIntelligenceService intelligenceService,
        Guid userId,
        ITravelIntelligenceExecutionPersistenceService? persistenceService = null)
    {
        var controller = new ItineraryValidationsController(
            validationService,
            intelligenceService,
            persistenceService ?? new RecordingPersistenceService(),
            new RecordingExecutionQueryService())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                        "TestAuth"))
                }
            }
        };
        return controller;
    }

    private sealed class RecordingValidationService(
        ItineraryValidationResponse? response) : IItineraryValidationService
    {
        public Guid? ReceivedUserId { get; private set; }

        public Task<(bool Succeeded, string? Error, ItineraryValidationResponse? Response)> ValidateAsync(
            ItineraryValidationRequest request,
            Guid authenticatedUserId,
            CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<ItineraryValidationResponse?> GetByIdAsync(
            Guid id,
            Guid authenticatedUserId,
            CancellationToken cancellationToken = default)
        {
            ReceivedUserId = authenticatedUserId;
            return Task.FromResult(response);
        }
    }

    private sealed class RecordingIntelligenceService : ITravelIntelligenceService
    {
        public ItineraryValidationResponse? ReceivedValidation { get; private set; }

        public int CallCount { get; private set; }

        public Task<TravelIntelligenceResponse> AnalyzeAsync(
            ItineraryValidationResponse validation,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ReceivedValidation = validation;
            return Task.FromResult(new TravelIntelligenceResponse
            {
                ValidationResultId = validation.Id,
                RiskLevel = validation.RiskLevel,
                IsFeasible = validation.IsFeasible,
                RequiresHumanApproval = validation.BlockingIssueCount > 0,
                RecommendedAction = TravelIntelligenceAction.ManualReview
            });
        }
    }

    private sealed class RecordingPersistenceService(
        TravelIntelligenceExecutionPersistenceResult? result = null)
        : ITravelIntelligenceExecutionPersistenceService
    {
        public Task<TravelIntelligenceExecutionPersistenceResult> PersistAsync(
            ItineraryValidationResponse validation,
            Guid requestedByUserId,
            TravelIntelligenceResponse recommendation,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result ?? new TravelIntelligenceExecutionPersistenceResult(
                true,
                null,
                Guid.NewGuid(),
                null));
    }

    private sealed class RecordingExecutionQueryService : ITravelIntelligenceExecutionQueryService
    {
        public Task<TravelIntelligenceExecutionPageResponse?> ListForOwnerAsync(Guid validationResultId, Guid ownerUserId, TravelIntelligenceExecutionQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<TravelIntelligenceExecutionPageResponse?>(null);

        public Task<TravelIntelligenceExecutionDetailResponse?> GetForOwnerAsync(Guid validationResultId, Guid executionId, Guid ownerUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TravelIntelligenceExecutionDetailResponse?>(null);

        public Task<TravelIntelligenceExecutionPageResponse> ListForStaffAsync(TravelIntelligenceExecutionQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TravelIntelligenceExecutionPageResponse());

        public Task<TravelIntelligenceExecutionDetailResponse?> GetForStaffAsync(Guid executionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<TravelIntelligenceExecutionDetailResponse?>(null);
    }
}
