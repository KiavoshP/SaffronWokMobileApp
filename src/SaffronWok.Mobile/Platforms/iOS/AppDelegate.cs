using Foundation;
using UIKit;

namespace SaffronWok.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
