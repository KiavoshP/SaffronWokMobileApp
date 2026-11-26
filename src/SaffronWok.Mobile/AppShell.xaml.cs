using Microsoft.Maui.Devices;
using SaffronWok.Mobile.Controls;
using SaffronWok.Shared;

namespace SaffronWok.Mobile;

public sealed partial class AppShell : Shell
{
    private readonly Tab menuTab;
    private readonly Tab locatorTab;
    private readonly MenuPage menu;
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
        FlyoutBehavior = useRail ? FlyoutBehavior.Locked : FlyoutBehavior.Disabled;

        menu = new MenuPage(catalog);
        homeTab = AddTab("Home", "home", new EventPage(catalog, OpenMenuAsync, ShowLocator, isHome: true));
        menuTab = AddTab("Menu", "menu", menu);
        AddTab("User", "user", new AccountPage(catalog.Api));
        AddTab("Rewards", "rewards", RewardsPage());
        locatorTab = AddTab("Locator", "locator", new EventPage(catalog, OpenMenuAsync, ShowLocator, isHome: false));
        Items.Add(navigation);
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
        Shell.SetTitleView(page, bar);
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

    private void ShowLocator() => navigation.CurrentItem = locatorTab;

    internal static Color ThemeColor(string key) => (Color)Application.Current!.Resources[key];

    private static ContentPage RewardsPage() => new()
    {
        Title = "Rewards",
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 16,
                Children =
                {
                    new Label { Text = "SaffronWok Rewards", FontSize = 24, FontAttributes = FontAttributes.Bold },
                    new Border
                    {
                        Padding = 20,
                        Stroke = new SolidColorBrush(ThemeColor("BorderAmber")),
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 24 },
                        Background = new LinearGradientBrush(
                            new GradientStopCollection
                            {
                                new(ThemeColor("AccentAmber"), 0),
                                new(ThemeColor("AccentEmber"), 0.5f),
                                new(ThemeColor("BgSurface"), 1)
                            }, new Point(0, 0), new Point(1, 1)),
                        Content = new VerticalStackLayout
                        {
                            Spacing = 8,
                            Children =
                            {
                                new SaffronLogoView { HorizontalOptions = LayoutOptions.Start },
                                new Label { Text = "EMBER CLUB", FontSize = 18, FontAttributes = FontAttributes.Bold },
                                new Label { Text = "Loyalty balances and redemptions are coming soon." }
                            }
                        }
                    }
                }
            }
        }
    };
}
