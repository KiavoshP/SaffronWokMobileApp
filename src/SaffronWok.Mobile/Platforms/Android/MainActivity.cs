using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;

namespace SaffronWok.Mobile;

[Activity(Theme = "@style/Maui.MainTheme", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
        ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity { }

[Application]
public sealed class MainApplication(nint handle, JniHandleOwnership ownership)
    : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
