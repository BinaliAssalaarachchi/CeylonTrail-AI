namespace CeylonTrail.Api.Models;

public class TravelIntelligenceExecutionStep
{
    public Guid Id { get; set; }

    public Guid TravelIntelligenceExecutionId { get; set; }

    public TravelIntelligenceExecution? TravelIntelligenceExecution { get; set; }

    public int Sequence { get; set; }

    public string StepId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Purpose { get; set; } = string.Empty;

    public string? PlannedToolName { get; set; }

    public string? ExecutedToolName { get; set; }

    public TravelIntelligenceExecutionStepStatus Status { get; set; }

    public int? DurationMs { get; set; }

    public string? ResultSummary { get; set; }

    public DateTime CreatedAt { get; set; }
}
