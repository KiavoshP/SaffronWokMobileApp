namespace SaffronWok.Shared;

public sealed record ServiceEvent(
    string Id, string Truck, string Name, string Address, string Hours,
    bool IsServing, double Latitude, double Longitude);

public sealed record MenuDish(string Name, decimal Price, bool SoldOut = false, string? Category = null,
    string? PhotoUrl = null);

public sealed record DailySpecial(string EventId, string Title, string Badge, string Tagline, decimal Price,
    string? PhotoUrl = null);

public sealed record DiscoveryBootstrap(IReadOnlyList<ServiceEvent> Events, EventMenu? Menu,
    IReadOnlyList<DailySpecial> Specials);

public sealed record EventMenu(string EventId, string? VenueNote, IReadOnlyList<MenuDish> Dishes);

public static class EventDistance
{
    public static double InMiles(double latitude, double longitude, ServiceEvent serviceEvent)
    {
        const double milesPerEarthRadius = 3958.76;
        const double radiansPerDegree = Math.PI / 180;
        var halfLatitudeDelta = (serviceEvent.Latitude - latitude) * radiansPerDegree / 2;
        var halfLongitudeDelta = (serviceEvent.Longitude - longitude) * radiansPerDegree / 2;
        var haversine = Math.Pow(Math.Sin(halfLatitudeDelta), 2)
            + Math.Cos(latitude * radiansPerDegree) * Math.Cos(serviceEvent.Latitude * radiansPerDegree)
            * Math.Pow(Math.Sin(halfLongitudeDelta), 2);
        return 2 * milesPerEarthRadius * Math.Asin(Math.Sqrt(Math.Clamp(haversine, 0, 1)));
    }
}

public sealed record NearbyEvent(ServiceEvent Event, double Miles);

public static class EventSuggestions
{
    public const double RadiusMiles = 5;

    public static IEnumerable<NearbyEvent> Find(
        IEnumerable<ServiceEvent> events, double latitude, double longitude) =>
        events.Select(item => new NearbyEvent(item, EventDistance.InMiles(latitude, longitude, item)))
            .Where(result => result.Miles <= RadiusMiles)
            .OrderByDescending(result => result.Event.IsServing)
            .ThenBy(result => result.Miles);
}
