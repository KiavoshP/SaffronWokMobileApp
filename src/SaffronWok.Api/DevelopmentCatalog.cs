using SaffronWok.Shared;

internal static class DevelopmentCatalog
{
    public static IReadOnlyList<ServiceEvent> Events { get; } =
    [
        new("smorgasburg", "Truck #1", "Smorgasburg Atlanta", "140 Forsyth St SW, Atlanta",
            "11 AM – 6 PM", true, 33.7508, -84.3957),
        new("pontoon", "Truck #2", "Pontoon Brewing Pop-Up", "8601 Dunwoody Pl, Sandy Springs",
            "1 PM – 7 PM", true, 33.9916, -84.3407),
        new("fourth-ward", "Truck #2", "Historic Fourth Ward Park", "680 Dallas St NE, Atlanta",
            "Upcoming · 1 PM – 7 PM", false, 33.7686, -84.3638)
    ];

    public static IReadOnlyDictionary<string, EventMenu> Menus { get; } = CreateMenus();

    private static Dictionary<string, EventMenu> CreateMenus()
    {
        var menus = new Dictionary<string, EventMenu>
        {
            ["smorgasburg"] = new("smorgasburg", null,
            [
                new("Saffron Chicken Kebab", 14.50m),
                new("Lemongrass Beef Skewer", 16.00m),
                new("Grilled Pork Skewer", 14.00m, SoldOut: true),
                new("Zesty Jade Combo Plate", 19.50m),
                new("Veggie & Torshi Plate", 12.50m),
                new("SaffronWok Duo Platter", 22.00m)
            ]),
            ["pontoon"] = new("pontoon", "Drinks are served inside the Pontoon taproom.",
            [
                new("Saffron Chicken Kebab", 14.50m),
                new("Lemongrass Beef Skewer", 16.00m),
                new("Grilled Pork Skewer", 14.00m),
                new("Veggie & Torshi Plate", 12.50m)
            ])
        };
        menus["fourth-ward"] = menus["pontoon"] with { EventId = "fourth-ward", VenueNote = null };
        return menus;
    }
}
