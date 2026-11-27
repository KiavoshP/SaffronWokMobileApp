namespace SaffronWok.Mobile;

public sealed partial class SplashPage : ContentPage
{
    private readonly DiscoveryCatalog catalog;
    private bool started;

    internal SplashPage(DiscoveryCatalog catalog)
    {
        this.catalog = catalog;
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (started)
            return;

        started = true;
        await Task.WhenAll(PreloadAsync(), AnimateAsync());
        await root.FadeToAsync(0, 420, Easing.CubicOut);
        if (Window is { } window && ReferenceEquals(window.Page, this))
            window.Page = new AppShell(catalog);
    }

    private async Task PreloadAsync()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            await catalog.LoadAsync(timeout.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
            or System.Text.Json.JsonException)
        {
            // The event page provides retry when the startup data cannot be loaded.
        }
    }

    private Task AnimateAsync() => Task.WhenAll(
        AnimateProgressAsync(),
        PulseAsync());

    private Task AnimateProgressAsync()
    {
        var complete = new TaskCompletionSource();
        new Animation(width => progressFill.WidthRequest = width, 0, 240)
            .Commit(progressFill, "SplashProgress", length: 2200, easing: Easing.CubicOut,
                finished: (_, _) => complete.TrySetResult());
        return complete.Task;
    }

    private async Task PulseAsync()
    {
        await Task.WhenAll(glow.ScaleToAsync(1.12, 900, Easing.SinInOut),
            emblem.TranslateToAsync(0, -8, 600, Easing.SinInOut));
        await Task.WhenAll(glow.ScaleToAsync(0.95, 900, Easing.SinInOut),
            emblem.TranslateToAsync(0, 0, 600, Easing.SinInOut));
    }
}
