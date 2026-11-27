using SaffronWok.Shared;

namespace SaffronWok.Mobile.Controls;

public sealed partial class LocationSwitcherBarView : ContentView
{
    public event EventHandler? SwitchRequested;

    public LocationSwitcherBarView() => InitializeComponent();

    internal void ShowLocation(ServiceEvent item)
    {
        truckName.Text = $"MENU FOR {item.Truck}";
        venue.Text = item.Name;
        truckDot.Color = AppShell.ThemeColor(item.Truck == "Truck #2" ? "AccentEmerald" : "AccentAmber");
    }

    private void SwitchTapped(object? sender, TappedEventArgs args) =>
        SwitchRequested?.Invoke(this, EventArgs.Empty);
}
