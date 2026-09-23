namespace CeylonTrail.Api.Models;

public class TripPreference
{
    public Guid Id { get; set; }

    public Guid TripId { get; set; }

    public string PreferenceType { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public Trip Trip { get; set; } = null!;
}
