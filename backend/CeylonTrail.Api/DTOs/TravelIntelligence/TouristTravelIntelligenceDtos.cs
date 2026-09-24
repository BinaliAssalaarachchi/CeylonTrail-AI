using CeylonTrail.Api.DTOs.ItineraryValidations;
using CeylonTrail.Api.Models;

namespace CeylonTrail.Api.DTOs.TravelIntelligence;

public enum TouristReviewStatus
{
    NotRequired,
    ApprovalRequired,
    Pending,
    Approved,
    Rejected
}

public sealed class TouristTravelIntelligenceOutcomeResponse
{
    public Guid TripId { get; set; }
    public Guid ExecutionId { get; set; }
    public ValidationRiskLevel RiskLevel { get; set; }
    public bool IsFeasible { get; set; }
    public ApprovalRecommendedAction RecommendedAction { get; set; }
    public string Summary { get; set; } = string.Empty;
    public bool RequiresHumanApproval { get; set; }
    public List<TravelIntelligenceRecommendation> Recommendations { get; set; } = new();
    public List<TravelIntelligenceAffectedItem> AffectedItems { get; set; } = new();
    public List<TravelIntelligenceAlternativeRecommendation> Alternatives { get; set; } = new();
    public List<TravelIntelligenceSafeWindowSuggestion> SafeWindows { get; set; } = new();
    public TouristReviewStatus ReviewStatus { get; set; }
    public ApprovalDecisionType? Decision { get; set; }
    public DateTime? RequestedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public DateTime AssessedAt { get; set; }
}
