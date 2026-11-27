using Microsoft.Maui.ApplicationModel;

namespace SaffronWok.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp() =>
        MauiApp.CreateBuilder()
            .UseMauiApp<DiscoveryApp>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("Inter-Regular.ttf", "InterRegular");
                fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
                fonts.AddFont("Inter-Bold.ttf", "InterBold");
                fonts.AddFont("Inter-ExtraBold.ttf", "InterExtraBold");
            })
            .Build();
}

public sealed partial class DiscoveryApp : Application
{
    private readonly DiscoveryCatalog catalog = new(new HttpClient
    {
        BaseAddress = ApiAddress(),
        Timeout = TimeSpan.FromSeconds(10)
    });

    public DiscoveryApp()
    {
        UserAppTheme = AppTheme.Dark;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(new SplashPage(catalog));

    private static Uri ApiAddress()
    {
#if ANDROID
        return new(Microsoft.Maui.Devices.DeviceInfo.Current.DeviceType == Microsoft.Maui.Devices.DeviceType.Physical
            ? "http://127.0.0.1:5000/"
            : "http://10.0.2.2:5000/");
#else
        return new("http://localhost:5000/");
#endif
    }
}
