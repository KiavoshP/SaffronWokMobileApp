using SaffronWok.Shared;

namespace SaffronWok.Mobile;

public sealed partial class MenuPage : ContentPage
{
    private readonly DiscoveryCatalog catalog;
    private ServiceEvent? selected;
    private int requestId;
    private IReadOnlyList<MenuDish> dishes = [];
    private string? category;

    internal MenuPage(DiscoveryCatalog catalog)
    {
        this.catalog = catalog;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (selected is not null)
            return;

        try
        {
            var events = await catalog.GetEventsAsync();
            if (selected is null && events.FirstOrDefault(item => item.IsServing) is { } serviceEvent)
                await SelectAsync(serviceEvent);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            if (selected is null)
                summary.Text = "Locations could not load. Try Switch.";
        }
    }

    internal async Task SelectAsync(ServiceEvent serviceEvent)
    {
        var request = ++requestId;
        if (selected?.Id != serviceEvent.Id)
            category = null;
        selected = serviceEvent;
        locationSwitcher.ShowLocation(serviceEvent);
        summary.Text = "Loading menu…";
        items.ItemsSource = null;
        categoryFilters.Children.Clear();
        venue.IsVisible = false;

        try
        {
            var menu = await catalog.GetMenuAsync(serviceEvent.Id);
            if (request != requestId)
                return;

            dishes = menu?.Dishes ?? [];
            RenderCategories();
            venueNote.Text = menu?.VenueNote;
            venue.IsVisible = !string.IsNullOrWhiteSpace(venueNote.Text);
            summary.Text = $"{menu?.Dishes.Count ?? 0} items at {serviceEvent.Name}";
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            if (request == requestId)
                summary.Text = "The menu could not load. Pull down to try again.";
        }
    }

    private void RenderCategories()
    {
        categoryFilters.Children.Clear();
        var categories = dishes.Select(dish => dish.Category).OfType<string>().Distinct().Order().ToArray();
        if (category is not null && !categories.Contains(category))
            category = null;

        items.ItemsSource = category is null ? dishes : dishes.Where(dish => dish.Category == category).ToArray();
        if (categories.Length == 0)
            return;

        foreach (var value in new string?[] { null }.Concat(categories))
        {
            var chip = new Button { Text = value ?? "All", FontSize = 12, Padding = new Thickness(10, 4) };
            if (value == category)
                chip.Style = (Style)Application.Current!.Resources["PrimaryButton"];

            chip.Clicked += (_, _) => { category = value; RenderCategories(); };
            categoryFilters.Children.Add(chip);
        }
    }

    private async void LocationClicked(object? sender, EventArgs args)
    {
        try
        {
            var events = await catalog.GetEventsAsync();
            if (events.Count == 0)
            {
                summary.Text = "No locations are currently listed.";
                return;
            }

            var labels = events.Select(item => $"{item.Name} · {item.Truck}").ToArray();
            var choice = await DisplayActionSheetAsync("Choose pickup location", "Cancel", null, labels);
            var index = Array.IndexOf(labels, choice);
            if (index >= 0)
                await SelectAsync(events[index]);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            summary.Text = "Locations could not load. Try again.";
        }
    }

    private async void RefreshMenu(object? sender, EventArgs args)
    {
        try
        {
            if (selected is { } serviceEvent)
                await SelectAsync(serviceEvent);
        }
        finally
        {
            refresh.IsRefreshing = false;
        }
    }
}
