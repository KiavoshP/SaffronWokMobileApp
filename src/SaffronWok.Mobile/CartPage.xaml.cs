namespace SaffronWok.Mobile;

public sealed partial class CartPage : ContentPage
{
    private readonly Action showMenu;

    internal CartPage(Action showMenu)
    {
        this.showMenu = showMenu;
        InitializeComponent();
        if (DemoData.Enabled)
        {
            emptyCart.IsVisible = false;
            sampleCart.IsVisible = true;
            BindableLayout.SetItemsSource(sampleLines, DemoData.Cart);
            sampleTotal.Text = $"Total · ${DemoData.Cart.Sum(line => line.Total):F2}";
        }
    }

    private async void BrowseClicked(object? sender, EventArgs args)
    {
        await Navigation.PopAsync();
        showMenu();
    }
}
