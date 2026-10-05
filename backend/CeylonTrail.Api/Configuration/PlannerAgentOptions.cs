namespace CeylonTrail.Api.Configuration;

public sealed class PlannerAgentOptions
{
    public const string SectionName = "PlannerAgent";

    public string BaseUrl { get; set; } = "http://localhost:8002";

    public int TimeoutSeconds { get; set; } = 60;
}
