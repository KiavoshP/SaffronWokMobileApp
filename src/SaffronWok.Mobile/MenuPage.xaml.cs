using System.ComponentModel;
using SaffronWok.Shared;

namespace SaffronWok.Mobile;

public sealed partial class MenuPage : ContentPage
{
    private readonly DiscoveryCatalog catalog;
    private ServiceEvent? selected;
    private int requestId;
    private IReadOnlyList<MenuDishCard> dishes = [];
    private string? category;
    private MenuDishCard? pendingHighlight;
    private bool visible;

    internal MenuPage(DiscoveryCatalog catalog)
    {
        this.catalog = catalog;
        InitializeComponent();
        items.SizeChanged += (_, _) => ShowFeaturedDish();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        visible = true;
        ShowFeaturedDish();
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

    protected override void OnDisappearing()
    {
        visible = false;
        base.OnDisappearing();
    }

    internal async Task SelectAsync(ServiceEvent serviceEvent, string? featuredDish = null)
    {
        var request = ++requestId;
        pendingHighlight = null;
        if (selected?.Id != serviceEvent.Id || featuredDish is not null)
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

            dishes = menu?.Dishes.Select(dish => new MenuDishCard(dish)).ToArray() ?? [];
            RenderCategories();
            if (featuredDish is not null)
            {
                pendingHighlight = dishes.FirstOrDefault(card =>
                    string.Equals(card.Dish.Name, featuredDish, StringComparison.OrdinalIgnoreCase));
                ShowFeaturedDish();
            }

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

    private void ShowFeaturedDish()
    {
        if (pendingHighlight is null || !visible || items.Handler is null || items.Width <= 0 || items.Height <= 0)
            return;

        Dispatcher.Dispatch(async () =>
        {
            if (pendingHighlight is not { } card || !visible || items.Handler is null)
                return;

            pendingHighlight = null;
            card.Highlight = true;
            items.ScrollTo(card, position: ScrollToPosition.Center, animate: true);
            await Task.Delay(TimeSpan.FromSeconds(2));
            card.Highlight = false;
        });
    }

    private void RenderCategories()
    {
        categoryFilters.Children.Clear();
        var categories = dishes.Select(dish => dish.Dish.Category).OfType<string>().Distinct().Order().ToArray();
        if (category is not null && !categories.Contains(category))
            category = null;

        items.ItemsSource = category is null ? dishes : dishes.Where(dish => dish.Dish.Category == category).ToArray();
        if (categories.Length == 0)
            return;

        foreach (var value in new string?[] { null }.Concat(categories))
        {
            var chip = new Button { Text = value ?? "All", FontSize = 12, Padding = new Thickness(10, 4) };
            if (value == category)
                chip.Style = (Style)Application.Current!.Resources["PrimaryButton"];

            chip.Clicked += (_, _) =>
            {
                pendingHighlight = null;
                category = value;
                RenderCategories();
            };
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


internal sealed class MenuDishCard(MenuDish dish) : INotifyPropertyChanged
{
    private bool highlight;
    public MenuDish Dish { get; } = dish;
    public ImageSource? Photo { get; } = DishPhoto.FromUrl(dish.PhotoUrl);
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Highlight
    {
        get => highlight;
        set
        {
            if (highlight == value)
                return;

            highlight = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Highlight)));
        }
    }
}
