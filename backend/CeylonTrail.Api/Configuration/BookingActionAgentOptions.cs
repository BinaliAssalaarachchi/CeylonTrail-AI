namespace CeylonTrail.Api.Configuration;

public sealed class BookingActionAgentOptions
{
    public const string SectionName = "BookingActionAgent";

    public string BaseUrl { get; set; } = "http://localhost:8004";

    public int TimeoutSeconds { get; set; } = 60;
}
