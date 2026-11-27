using SaffronWok.Shared;

namespace SaffronWok.Mobile;

public sealed partial class HomePage : ContentPage
{
    private readonly DiscoveryCatalog catalog;
    private readonly Func<ServiceEvent, Task> openMenu;
    private readonly Action<ServiceEvent?> openLocator;
    private CancellationTokenSource? rotation;
    private bool loading;

    internal HomePage(DiscoveryCatalog catalog, Func<ServiceEvent, Task> openMenu, Action<ServiceEvent?> openLocator)
    {
        this.catalog = catalog;
        this.openMenu = openMenu;
        this.openLocator = openLocator;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (rotation is not null)
            return;

        rotation = new CancellationTokenSource();
        await Task.WhenAll(LoadAsync(), RotateAsync(rotation.Token));
    }

    protected override void OnDisappearing()
    {
        rotation?.Cancel();
        rotation?.Dispose();
        rotation = null;
        base.OnDisappearing();
    }

    private async Task LoadAsync(bool refresh = false)
    {
        if (loading)
            return;

        loading = true;
        try
        {
            var events = await catalog.GetEventsAsync(refresh);
            var locations = events.Where(item => item.IsServing).Select(item => item.Truck).Distinct().Count();
            liveLocations.Text = locations == 1 ? "1 location live" : $"{locations} locations live";
            SpecialsCarousel.ItemsSource = catalog.Specials.Select(item =>
                (Special: item, Event: events.FirstOrDefault(serviceEvent => serviceEvent.Id == item.EventId)))
                .Where(item => item.Event is not null).Select(item => new DailySpecialModel(item.Special, item.Event!)).ToArray();
            specialsSection.IsVisible = SpecialsCarousel.ItemsSource is DailySpecialModel[] { Length: > 0 };
            upcomingEvents.ItemsSource = events.OrderByDescending(item => item.IsServing)
                .Select(item => new UpcomingEventModel(item)).ToArray();
            status.IsVisible = events.Count == 0;
            status.Text = "No events are currently listed.";
            retry.IsVisible = status.IsVisible;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            status.Text = "Events could not load.";
            status.IsVisible = retry.IsVisible = true;
        }
        finally
        {
            loading = false;
        }
    }

    private async Task RotateAsync(CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellation))
            {
                if (!SpecialsCarousel.IsDragging)
                    Step(1);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
    }

    private void Step(int direction)
    {
        if (SpecialsCarousel.ItemsSource is DailySpecialModel[] { Length: > 1 } items)
            SpecialsCarousel.Position = (SpecialsCarousel.Position + direction + items.Length) % items.Length;
    }

    private async void RetryClicked(object? sender, EventArgs e) => await LoadAsync(refresh: true);
    private void PreviousClicked(object? sender, EventArgs e) => Step(-1);
    private void NextClicked(object? sender, EventArgs e) => Step(1);

    private void LocatorTapped(object? sender, TappedEventArgs e) =>
        openLocator((e.Parameter as UpcomingEventModel)?.Event);

    private async void SpecialClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: DailySpecialModel special })
            await openMenu(special.Event);
    }
}

internal sealed record DailySpecialModel(DailySpecial Special, ServiceEvent Event)
{
    public string Title => Special.Title;
    public string Badge => Special.Badge;
    public string Tagline => Special.Tagline;
    public decimal Price => Special.Price;
    public string LocationLabel => Event.Name;
    public Color GradientStart => Color.FromArgb(Event.Truck == "Truck #2" ? "#064E3B" : "#7C2D12");
    public Color GradientMid => Color.FromArgb(Event.Truck == "Truck #2" ? "#073B32" : "#431407");
}

internal sealed record UpcomingEventModel(ServiceEvent Event)
{
    public string Name => Event.Name;
    public string Location => Event.Address;
    public string Hours => Event.Hours;
    public string TruckShortName => Event.Truck;
    public bool IsLive => Event.IsServing;
    public string DateBadge => IsLive ? "SERVING NOW" : "UPCOMING";
    public Color TruckFill => AppShell.ThemeColor(Event.Truck == "Truck #2" ? "AccentEmeraldTint" : "AccentAmberTint");
    public Brush TruckStroke => new SolidColorBrush(AppShell.ThemeColor(Event.Truck == "Truck #2" ? "BorderEmerald" : "BorderAmber"));
    public Color TruckText => AppShell.ThemeColor(Event.Truck == "Truck #2" ? "AccentEmeraldSoft" : "AccentAmberSoft");
}
