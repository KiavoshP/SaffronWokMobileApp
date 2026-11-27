using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using SaffronWok.Shared;

namespace SaffronWok.Mobile;

public sealed partial class EventPage : ContentPage
{
    private readonly DiscoveryCatalog catalog;
    private readonly Func<ServiceEvent, Task> openMenu;
    private Location? deviceLocation;
    private bool loading;
    private string? truckFilter;
    private string? selectedId;

    internal EventPage(DiscoveryCatalog catalog, Func<ServiceEvent, Task> openMenu)
    {
        this.catalog = catalog;
        this.openMenu = openMenu;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async void RefreshClicked(object? sender, EventArgs args) => await LoadAsync(refresh: true);

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

        var nearby = deviceLocation is { } location
            ? EventSuggestions.Find(catalog.Events, location.Latitude, location.Longitude).FirstOrDefault()
            : null;
        if (nearby is not null)
        {
            truckFilter = null;
            selectedId = nearby.Event.Id;
        }

        RenderEvents();
        if (deviceLocation is null)
            Status("Location is unavailable. You can still browse all events.");
        else if (nearby is null && catalog.Events.Count > 0)
            Status("No events within 5 miles. You can still browse all events.");
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
        var events = catalog.Events;
        var liveCount = events.Where(item => item.IsServing).Select(item => item.Truck).Distinct().Count();
        serviceCount.Text = liveCount == 1 ? "1 location live" : $"{liveCount} locations live";
        Status(events.Count == 0 ? "No events are currently listed." : null);
        RenderLocator(events);
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
        selectedEventPanel.Children.Add(CreateEventCard(selectedEvent));

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

    internal void FocusEvent(ServiceEvent item)
    {
        truckFilter = null;
        SelectEvent(item);
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

    private Border CreateEventCard(ServiceEvent item)
    {
        var truckColor = AppShell.ThemeColor(item.Truck == "Truck #2" ? "AccentEmeraldSoft" : "AccentAmberSoft");
        var button = new Button { Text = "View menu", HorizontalOptions = LayoutOptions.Start };
        button.Clicked += async (_, _) => await openMenu(item);

        var actions = new HorizontalStackLayout { Spacing = 8, Children = { button } };
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
                        Text = $"{(item.IsServing ? "Serving now" : "Upcoming")} · {item.Truck}",
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
