using Microsoft.Maui.Devices;
using SaffronWok.Mobile.Controls;
using SaffronWok.Shared;

namespace SaffronWok.Mobile;

public sealed partial class AppShell : Shell
{
    private readonly Tab menuTab;
    private readonly Tab locatorTab;
    private readonly MenuPage menu;
    private readonly EventPage locator;
    private readonly Tab homeTab;
    private readonly ShellItem navigation;
    private readonly bool useRail;

    internal AppShell(DiscoveryCatalog catalog)
    {
        InitializeComponent();

        var idiom = DeviceInfo.Idiom;
        useRail = idiom == DeviceIdiom.Tablet || idiom == DeviceIdiom.Desktop;
        navigation = useRail
            ? new FlyoutItem { FlyoutDisplayOptions = FlyoutDisplayOptions.AsMultipleItems }
            : new TabBar();
        FlyoutBehavior = FlyoutBehavior.Disabled;

        menu = new MenuPage(catalog);
        locator = new EventPage(catalog, OpenMenuAsync);
        homeTab = AddTab("Home", "home", new HomePage(catalog, OpenMenuAsync, ShowLocator));
        menuTab = AddTab("Menu", "menu", menu);
        AddTab("User", "user", new AccountPage(catalog.Api));
        AddTab("Rewards", "rewards", new RewardsPage());
        locatorTab = AddTab("Locator", "locator", locator);
        var splash = new SplashPage(catalog, ShowMainNavigation);
        SetNavBarIsVisible(splash, false);
        SetTabBarIsVisible(splash, false);
        var startup = new TabBar { Route = "startup" };
        SetFlyoutItemIsVisible(startup, false);
        var startupTab = new Tab { Route = "startup-tab" };
        startupTab.Items.Add(new ShellContent { Route = "splash", Content = splash });
        startup.Items.Add(startupTab);

        Items.Add(startup);
        Items.Add(navigation);
        CurrentItem = startup;
    }

    private void ShowMainNavigation()
    {
        CurrentItem = navigation;
        FlyoutBehavior = useRail ? FlyoutBehavior.Locked : FlyoutBehavior.Disabled;
    }

    private Tab AddTab(string title, string route, ContentPage page)
    {
        var bar = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
        bar.Children.Add(new SaffronLogoView());
        var label = new Label { FontSize = 15, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center };
        label.FormattedText = new FormattedString
        {
            Spans =
            {
                new Span { Text = "Saffron", TextColor = ThemeColor("TextPrimary") },
                new Span { Text = "Wok", TextColor = ThemeColor("AccentAmberBright") }
            }
        };
        bar.Children.Add(label);
        var home = new TapGestureRecognizer();
        home.Tapped += (_, _) => navigation.CurrentItem = homeTab;
        bar.GestureRecognizers.Add(home);
        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };
        var cart = new Button
        {
            Text = "Cart",
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center
        };
        cart.Clicked += async (_, _) =>
        {
            cart.IsEnabled = false;
            try
            {
                await page.Navigation.PushAsync(new CartPage(() => navigation.CurrentItem = menuTab));
            }
            finally
            {
                cart.IsEnabled = true;
            }
        };
        Grid.SetColumn(cart, 1);
        if (DemoData.Enabled)
            bar.Children.Add(new Label { Text = "DEMO", FontSize = 10, TextColor = ThemeColor("AccentAmberBright"),
                VerticalOptions = LayoutOptions.Center });

        header.Children.Add(bar);
        header.Children.Add(cart);
        Shell.SetTitleView(page, header);
        Shell.SetTabBarIsVisible(page, !useRail);

        var tab = new Tab { Title = title, Route = route, Icon = "nav_" + route + ".png" };
        tab.Items.Add(new ShellContent { Route = route + "-screen", Content = page });
        navigation.Items.Add(tab);
        return tab;
    }

    private async Task OpenMenuAsync(ServiceEvent serviceEvent)
    {
        navigation.CurrentItem = menuTab;
        await menu.SelectAsync(serviceEvent);
    }

    private void ShowLocator(ServiceEvent? serviceEvent)
    {
        if (serviceEvent is not null)
            locator.FocusEvent(serviceEvent);

        navigation.CurrentItem = locatorTab;
    }

    internal static Color ThemeColor(string key) => (Color)Application.Current!.Resources[key];
}
