using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using SaffronWok.Shared;

namespace SaffronWok.Mobile;

public sealed partial class EventPage : ContentPage
{
    private readonly DiscoveryCatalog catalog;
    private readonly Func<ServiceEvent, Task> openMenu;
    private readonly Action showLocator;
    private Location? deviceLocation;
    private bool loading;
    private readonly bool isHome;
    private string? truckFilter;
    private string? selectedId;
    private CancellationTokenSource? rotation;

    internal EventPage(DiscoveryCatalog catalog, Func<ServiceEvent, Task> openMenu, Action showLocator, bool isHome)
    {
        this.catalog = catalog;
        this.openMenu = openMenu;
        this.showLocator = showLocator;
        this.isHome = isHome;
        InitializeComponent();
        Title = isHome ? "Home" : "Locator";
        locatorLink.IsVisible = isHome;
        if (!isHome)
        {
            headline.Text = "Food Truck Locator";
            eyebrow.Text = "LIVE & UPCOMING SCHEDULE";
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!isHome || rotation is not null)
        {
            await LoadAsync();
            return;
        }

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

    private async Task RotateAsync(CancellationToken cancellation)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellation))
            {
                var count = catalog.Specials.Count;
                if (count > 1 && !specials.IsDragging)
                    specials.Position = (specials.Position + 1) % count;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
    }

    private async void SpecialClicked(object? sender, EventArgs args)
    {
        if (sender is Button { CommandParameter: DailySpecial special } &&
            catalog.Events.FirstOrDefault(item => item.Id == special.EventId) is { } serviceEvent)
            await openMenu(serviceEvent);
    }

    private async void RefreshClicked(object? sender, EventArgs args) => await LoadAsync(refresh: true);
    private void LocatorClicked(object? sender, EventArgs args) => showLocator();

    private async void LocateClicked(object? sender, EventArgs args)
    {
        try
        {
            deviceLocation = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
        }
        catch (Exception exception) when (exception is PermissionException or FeatureNotSupportedException
            or FeatureNotEnabledException or OperationCanceledException)
        {
            deviceLocation = null;
        }

        if (!isHome && deviceLocation is { } location)
        {
            var nearby = EventSuggestions.Find(catalog.Events, location.Latitude, location.Longitude).FirstOrDefault();
            if (nearby is not null)
            {
                truckFilter = null;
                selectedId = nearby.Event.Id;
            }
        }

        RenderEvents();
        if (deviceLocation is null)
            Status("Location is unavailable. You can still browse all events.");
    }

    private async Task LoadAsync(bool refresh = false)
    {
        if (loading)
            return;

        loading = true;
        try
        {
            Status("Loading events…");
            await catalog.GetEventsAsync(refresh);
            RenderEvents();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Status("Events could not load. Try refreshing.");
        }
        finally
        {
            loading = false;
        }
    }

    private void RenderEvents()
    {
        eventsPanel.Children.Clear();
        if (isHome)
        {
            specials.ItemsSource = catalog.Specials;
            specialsSection.IsVisible = catalog.Specials.Count > 0;
        }

        var events = catalog.Events;
        var liveCount = events.Where(item => item.IsServing).Select(item => item.Truck).Distinct().Count();
        serviceCount.Text = liveCount == 1 ? "1 location live" : $"{liveCount} locations live";
        Status(events.Count == 0 ? "No events are currently listed." : null);
        if (isHome && deviceLocation is { } location && events.Count > 0)
        {
            var suggestions = EventSuggestions.Find(events, location.Latitude, location.Longitude).ToArray();
            if (suggestions.Length == 0)
                Status("No events within 5 miles. You can still browse all events.");
            else
            {
                AddSection("NEARBY EVENTS");
                foreach (var result in suggestions)
                    AddEvent(result.Event, result.Miles);
            }
        }

        if (!isHome)
        {
            RenderLocator(events);
            return;
        }

        if (events.Count > 0)
            AddSection("LIVE & UPCOMING EVENTS");

        foreach (var item in events.OrderByDescending(item => item.IsServing))
            AddEvent(item, null);
    }

    private void RenderLocator(IReadOnlyList<ServiceEvent> allEvents)
    {
        truckFilters.Children.Clear();
        selectedEventPanel.Children.Clear();
        mapPins.Children.Clear();
        truckFilters.IsVisible = mapCard.IsVisible = allEvents.Count > 0;
        if (allEvents.Count == 0)
            return;

        var trucks = allEvents.Select(item => item.Truck).Distinct().Order().ToArray();
        if (truckFilter is not null && !trucks.Contains(truckFilter))
            truckFilter = null;

        foreach (var truck in new string?[] { null }.Concat(trucks))
        {
            var filter = new Button { Text = truck ?? "All Trucks", FontSize = 11, Padding = new Thickness(8, 4) };
            if (truck == truckFilter)
                filter.Style = (Style)Application.Current!.Resources["PrimaryButton"];
            filter.Clicked += (_, _) => { truckFilter = truck; RenderEvents(); };
            truckFilters.Children.Add(filter);
        }

        var events = allEvents.Where(item => truckFilter is null || item.Truck == truckFilter)
            .OrderByDescending(item => item.IsServing).ToArray();
        var selectedEvent = events.FirstOrDefault(item => item.Id == selectedId) ?? events[0];
        selectedId = selectedEvent.Id;
        selectedEventPanel.Children.Add(CreateEventCard(selectedEvent, null));

        var west = events.Min(item => item.Longitude);
        var east = events.Max(item => item.Longitude);
        var south = events.Min(item => item.Latitude);
        var north = events.Max(item => item.Latitude);
        foreach (var item in events)
        {
            var pin = new Button
            {
                Text = item.Truck == "Truck #2" ? "2" : "1",
                WidthRequest = 44,
                HeightRequest = 44,
                CornerRadius = 22,
                Padding = 0,
                BackgroundColor = AppShell.ThemeColor(item.Truck == "Truck #2" ? "AccentEmerald" : "AccentAmber"),
                TextColor = AppShell.ThemeColor("TextOnAccent"),
                Opacity = selectedId == item.Id ? 1 : 0.75
            };
            SemanticProperties.SetDescription(pin, $"{item.Name}, {item.Truck}");
            pin.Clicked += (_, _) => SelectEvent(item);
            var x = east == west ? 0.5 : 0.15 + 0.7 * (item.Longitude - west) / (east - west);
            var y = north == south ? 0.5 : 0.15 + 0.7 * (north - item.Latitude) / (north - south);
            AbsoluteLayout.SetLayoutFlags(pin, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.PositionProportional);
            AbsoluteLayout.SetLayoutBounds(pin, new Rect(x, y, 44, 44));
            mapPins.Children.Add(pin);

            var row = new Button
            {
                Text = $"{item.Name} · {item.Truck}",
                FontSize = 12,
                HorizontalOptions = LayoutOptions.Fill
            };
            row.Clicked += (_, _) => SelectEvent(item);
            eventsPanel.Children.Add(row);
        }
    }

    private void SelectEvent(ServiceEvent item)
    {
        selectedId = item.Id;
        RenderEvents();
    }

    private void Status(string? message)
    {
        status.Text = message;
        status.IsVisible = message is not null;
    }

    private void AddSection(string title) => eventsPanel.Children.Add(new Label
    {
        Text = title,
        FontSize = 12,
        FontAttributes = FontAttributes.Bold,
        TextColor = AppShell.ThemeColor("AccentAmberSoft")
    });

    private void AddEvent(ServiceEvent item, double? miles) => eventsPanel.Children.Add(CreateEventCard(item, miles));

    private Border CreateEventCard(ServiceEvent item, double? miles)
    {
        var truckColor = AppShell.ThemeColor(item.Truck == "Truck #2" ? "AccentEmeraldSoft" : "AccentAmberSoft");
        var button = new Button { Text = "View menu", HorizontalOptions = LayoutOptions.Start };
        button.Clicked += async (_, _) => await openMenu(item);

        var actions = new HorizontalStackLayout { Spacing = 8, Children = { button } };
        if (!isHome)
        {
            var directions = new Button { Text = "Get directions" };
            directions.Clicked += async (_, _) =>
            {
                try
                {
                    await Microsoft.Maui.ApplicationModel.Map.Default.OpenAsync(item.Latitude, item.Longitude,
                        new MapLaunchOptions { Name = item.Name, NavigationMode = NavigationMode.Driving });
                }
                catch (Exception exception) when (exception is FeatureNotSupportedException or InvalidOperationException)
                {
                    Status("Directions are unavailable on this device.");
                }
            };
            actions.Children.Add(directions);
        }

        return new Border
        {
            Padding = 14,
            BackgroundColor = AppShell.ThemeColor("BgSurface"),
            Stroke = new SolidColorBrush(AppShell.ThemeColor("BorderStrong")),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Content = new VerticalStackLayout
            {
                Spacing = 7,
                Children =
                {
                    new Label
                    {
                        Text = $"{(item.IsServing ? "Serving now" : "Upcoming")} · {item.Truck}"
                            + (miles is { } distance ? $" · {distance:0.0} mi" : ""),
                        FontAttributes = FontAttributes.Bold, FontSize = 11, TextColor = truckColor
                    },
                    new Label { Text = item.Name, FontSize = 16, FontAttributes = FontAttributes.Bold },
                    new Label { Text = item.Address, TextColor = AppShell.ThemeColor("TextMuted") },
                    new Label { Text = item.Hours, TextColor = AppShell.ThemeColor("TextMuted") },
                    actions
                }
            }
        };
    }
}
