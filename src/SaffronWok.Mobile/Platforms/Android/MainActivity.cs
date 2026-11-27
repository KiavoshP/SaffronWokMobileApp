using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.Graphics;
using AndroidX.Core.View;
using NativeView = Android.Views.View;

namespace SaffronWok.Mobile;

[Activity(Theme = "@style/Maui.MainTheme.NoActionBar", MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
        ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Shell owns the toolbar. Fit its entire host below the system bars and above gesture navigation.
        if (Window is not { } window || FindViewById<NativeView>(Android.Resource.Id.Content) is not { } content)
            return;

        content.SetBackgroundColor(Android.Graphics.Color.ParseColor("#09090B"));
        var controller = WindowCompat.GetInsetsController(window, window.DecorView);
        if (controller is not null)
        {
            controller.AppearanceLightStatusBars = false;
            controller.AppearanceLightNavigationBars = false;
        }

        ViewCompat.SetOnApplyWindowInsetsListener(content, new ShellInsetsListener());
        ViewCompat.RequestApplyInsets(content);
    }

    private sealed class ShellInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        public WindowInsetsCompat? OnApplyWindowInsets(NativeView? view, WindowInsetsCompat? insets)
        {
            if (view is null || insets is null)
                return insets;

            var edges = WindowInsetsCompat.Type.SystemBars() | WindowInsetsCompat.Type.DisplayCutout();
            var padding = insets.GetInsets(edges);
            if (padding is null)
                return insets;

            view.SetPadding(padding.Left, padding.Top, padding.Right, padding.Bottom);
            return new WindowInsetsCompat.Builder(insets).SetInsets(edges, Insets.None)?.Build() ?? insets;
        }
    }
}

[Application]
public sealed class MainApplication(nint handle, JniHandleOwnership ownership)
    : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
