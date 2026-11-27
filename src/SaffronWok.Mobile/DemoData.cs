using SaffronWok.Shared;

namespace SaffronWok.Mobile;

internal static class DemoData
{
    public static bool Enabled { get; } =
#if SAFFRON_DEMO
        true;
#else
        false;
#endif

    public static CustomerProfile Customer { get; } =
        new(Guid.Parse("db5a665a-068b-41c8-90bd-36f2a60534e1"), "Maya Rostami", "+14045550123");

    private static readonly MenuDish Chicken = new("Saffron Chicken Kebab", 14.50m, Category: "Skewers");
    private static readonly MenuDish Beef = new("Lemongrass Beef Skewer", 16.00m, Category: "Skewers");
    private static readonly MenuDish Pork = new("Charcoal Grilled Pork", 14.00m, Category: "Skewers");
    private static readonly MenuDish Veggie = new("Veggie & Torshi Plate", 12.50m, Category: "Vegetarian");
    private static readonly MenuDish Jade = new("Zesty Jade Combo Plate", 19.50m, Category: "Platters");

    public static IReadOnlyDictionary<string, EventMenu> Menus { get; } = new Dictionary<string, EventMenu>
    {
        ["sample-market"] = new("sample-market", null, [Chicken, Beef, Pork with { SoldOut = true }, Veggie, Jade]),
        ["sample-taproom"] = new("sample-taproom", "Drinks are served inside the taproom.", [Chicken, Beef, Pork, Veggie]),
        ["sample-park"] = new("sample-park", null, [Chicken, Veggie, Jade]),
        ["sample-night-market"] = new("sample-night-market", null, [Beef, Pork, Veggie])
    };

    public static DiscoveryBootstrap Bootstrap { get; } = new(
        [
            new("sample-market", "Truck #1", "Sample Downtown Market", "140 Forsyth St SW, Atlanta",
                "Today · 11 AM – 6 PM", true, 33.7508, -84.3957),
            new("sample-taproom", "Truck #2", "Sample Taproom Pop-Up", "8601 Dunwoody Pl, Sandy Springs",
                "Today · 1 PM – 7 PM", true, 33.9916, -84.3407),
            new("sample-park", "Truck #1", "Sample Fourth Ward Picnic", "680 Dallas St NE, Atlanta",
                "Saturday · 12 PM – 5 PM", false, 33.7686, -84.3638),
            new("sample-night-market", "Truck #2", "Sample Night Market", "675 Ponce De Leon Ave NE, Atlanta",
                "Sunday · 4 PM – 9 PM", false, 33.7725, -84.3656)
        ],
        Menus["sample-market"],
        [
            new("sample-market", Chicken.Name, "CHEF’S PICK", "Saffron-marinated chicken fresh off the charcoal.", Chicken.Price),
            new("sample-taproom", Beef.Name, "FAN FAVORITE", "Charcoal-seared beef with bright lemongrass.", Beef.Price),
            new("sample-market", Jade.Name, "LIMITED DROP", "A generous platter with herbs and a hint of smoke.", Jade.Price),
            new("sample-taproom", Pork.Name, "SMOKY CLASSIC", "Crisp edges and tender charcoal-grilled pork.", Pork.Price)
        ]);

    public static IReadOnlyList<DemoLine> Cart { get; } =
        [new(Chicken.Name, 2, Chicken.Price, "With café-style pickled vegetables"), new(Veggie.Name, 1, Veggie.Price, "Warm flatbread on the side")];

    public static IReadOnlyList<DemoOrder> Orders { get; } =
        [
            new("SW-1042", "Preparing", "Truck #1 · Sample Downtown Market", "Today · 12:34 PM", Cart),
            new("SW-1041", "Ready for pickup", "Truck #2 · Sample Taproom Pop-Up", "Today · 12:18 PM",
                [new(Beef.Name, 1, Beef.Price, "With herbs and lime")]),
            new("SW-1008", "Completed", "Truck #1 · Sample Fourth Ward Picnic", "Last Saturday · 2:15 PM",
                [new(Jade.Name, 1, Jade.Price, "House platter")])
        ];

    public const int RewardPoints = 320;
    public const int RewardGoal = 500;
    public static IReadOnlyList<DemoPerk> Perks { get; } =
        [
            new("Free Craft Drink", "Choose a participating craft drink.", 150),
            new("Free Single Skewer", "Pick one skewer from the live menu.", 300),
            new("Free Zesty Jade Combo", "Includes the platter and a craft drink.", RewardGoal)
        ];
}

internal sealed record DemoLine(string Name, int Quantity, decimal UnitPrice, string Note)
{
    public string QuantityLabel => $"{Quantity} × {Name}";
    public decimal Total => Quantity * UnitPrice;
}

internal sealed record DemoOrder(string Number, string Status, string Location, string Placed, IReadOnlyList<DemoLine> Lines)
{
    public decimal Total => Lines.Sum(line => line.Total);
    public bool IsActive => Status != "Completed";
}

internal sealed record DemoPerk(string Name, string Description, int Points);
