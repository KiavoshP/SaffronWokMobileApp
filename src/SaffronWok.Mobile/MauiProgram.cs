using System.Net.Http.Json;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;
using SaffronWok.Shared;

namespace SaffronWok.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp() =>
        MauiApp.CreateBuilder().UseMauiApp<DiscoveryApp>().Build();
}

public sealed class DiscoveryApp : Application
{
    protected override Window CreateWindow(IActivationState? activationState) =>
        new(new NavigationPage(new DiscoveryPage()));
}

internal sealed class DiscoveryPage : ContentPage
{
    private static readonly Color Amber = Color.FromArgb("#F59E0B");
    private static readonly Color Secondary = Color.FromArgb("#A1A1AA");
    private readonly HttpClient api = new() { BaseAddress = ApiAddress(), Timeout = TimeSpan.FromSeconds(10) };
    private readonly VerticalStackLayout eventsPanel = new() { Spacing = 10 };
    private readonly VerticalStackLayout menuPanel = new() { Spacing = 10 };
    private readonly Label status = new() { TextColor = Secondary };
    private IReadOnlyList<ServiceEvent> events = [];
    private Location? deviceLocation;
    private int menuRequest;

    public DiscoveryPage()
    {
        BackgroundColor = Color.FromArgb("#09090B");
        var refresh = new Button { Text = "Refresh events", BackgroundColor = Amber, TextColor = Colors.Black };
        refresh.Clicked += async (_, _) => await LoadEventsAsync();
        var nearby = new Button { Text = "Find nearby events" };
        nearby.Clicked += async (_, _) => await LocateAsync();
        var verifyPhone = new Button { Text = "Verify phone number" };
        verifyPhone.Clicked += async (_, _) => await Navigation.PushAsync(new PhoneVerificationPage(api));
        Content = new ScrollView
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24, 40, 24, 24),
                Spacing = 16,
                Children =
                {
                    Heading("SaffronWok", 34), Heading("Find our charcoal kitchen", 22),
                    new Label { Text = "Sample events and menus", TextColor = Secondary },
                    new Label { Text = "Mobile ordering coming soon. Order in person at the truck.", TextColor = Amber },
                    refresh, nearby, status, eventsPanel, menuPanel, verifyPhone
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (events.Count == 0)
            await LoadEventsAsync();
    }

    private async Task LoadEventsAsync()
    {
        status.Text = "Loading events…";
        try
        {
            events = await api.GetFromJsonAsync<ServiceEvent[]>("api/events") ?? [];
            RenderEvents();
        }
        catch (HttpRequestException)
        {
            status.Text = "Events could not load. Try refreshing.";
        }
        catch (TaskCanceledException)
        {
            status.Text = "Loading events timed out. Try refreshing.";
        }
    }

    private async Task LocateAsync()
    {
        try
        {
            deviceLocation = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
        }
        catch (Exception exception) when (exception is PermissionException or
            FeatureNotSupportedException or FeatureNotEnabledException or OperationCanceledException)
        {
            deviceLocation = null;
        }

        RenderEvents();
        if (deviceLocation is null)
            status.Text = "Location is unavailable. Choose an event to see its menu.";
    }

    private void RenderEvents()
    {
        eventsPanel.Children.Clear();
        status.Text = events.Count == 0 ? "No events are currently listed." : "Choose an event to see its menu.";
        var location = deviceLocation;
        var nearby = location is null ? [] :
            EventSuggestions.Find(events, location.Latitude, location.Longitude).ToArray();
        if (location is not null && events.Count > 0)
        {
            if (nearby.Length == 0)
                status.Text = "No events within 5 miles. You can still browse all events.";

            foreach (var result in nearby)
            {
                var suggestion = new Button
                {
                    Text = $"{(result.Event.IsServing ? "Serving now" : "Upcoming")} · {result.Event.Name} · {result.Miles:0.0} mi · View menu"
                };
                suggestion.Clicked += async (_, _) => await LoadMenuAsync(result.Event);
                eventsPanel.Children.Add(suggestion);
            }

            eventsPanel.Children.Add(Heading("All events", 18));
        }

        foreach (var item in events.OrderByDescending(item => item.IsServing))
        {
            var distance = location is null ? "" :
                $" · {EventDistance.InMiles(location.Latitude, location.Longitude, item):0.0} mi";
            var button = new Button { Text = $"{item.Name} · {item.Truck}{distance}" };
            button.Clicked += async (_, _) => await LoadMenuAsync(item);
            eventsPanel.Children.Add(button);
            eventsPanel.Children.Add(new Label
            {
                Text = $"{(item.IsServing ? "Serving now" : "Upcoming")} · {item.Hours}\n{item.Address}",
                TextColor = Secondary
            });
        }
    }

    private async Task LoadMenuAsync(ServiceEvent item)
    {
        var request = ++menuRequest;
        menuPanel.Children.Clear();
        menuPanel.Children.Add(Heading(item.Name, 24));
        var message = new Label { Text = "Loading menu…", TextColor = Secondary };
        menuPanel.Children.Add(message);
        try
        {
            var menu = await api.GetFromJsonAsync<EventMenu>($"api/events/{Uri.EscapeDataString(item.Id)}/menu");
            if (request != menuRequest)
                return;

            message.Text = menu?.VenueNote ?? "";
            message.IsVisible = !string.IsNullOrEmpty(message.Text);
            if (menu is null)
                return;

            foreach (var dish in menu.Dishes)
            {
                menuPanel.Children.Add(new Label
                {
                    Text = $"{dish.Name} · ${dish.Price:0.00}{(dish.SoldOut ? " · Sold out" : "")}",
                    TextColor = dish.SoldOut ? Secondary : Colors.White
                });
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            if (request == menuRequest)
                message.Text = "Menu could not load. Select the event to try again.";
        }
    }

    private static Label Heading(string text, double size) =>
        new() { Text = text, FontSize = size, FontAttributes = FontAttributes.Bold, TextColor = Colors.White };

    private static Uri ApiAddress()
    {
#if ANDROID
        return new("http://10.0.2.2:5000/");
#else
        return new("http://localhost:5000/");
#endif
    }
}
