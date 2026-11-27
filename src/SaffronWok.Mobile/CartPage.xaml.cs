namespace SaffronWok.Mobile;

public sealed partial class CartPage : ContentPage
{
    private readonly Action showMenu;

    internal CartPage(Action showMenu)
    {
        this.showMenu = showMenu;
        InitializeComponent();
    }

    private async void BrowseClicked(object? sender, EventArgs args)
    {
        await Navigation.PopAsync();
        showMenu();
    }
}
