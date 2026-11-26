namespace SaffronWok.Mobile.Controls;

public sealed partial class SaffronLogoView : ContentView
{
    public static readonly BindableProperty SizeProperty =
        BindableProperty.Create(nameof(Size), typeof(double), typeof(SaffronLogoView), 28d);

    public static readonly BindableProperty HasGlowProperty =
        BindableProperty.Create(nameof(HasGlow), typeof(bool), typeof(SaffronLogoView), false);

    public SaffronLogoView() => InitializeComponent();

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public bool HasGlow
    {
        get => (bool)GetValue(HasGlowProperty);
        set => SetValue(HasGlowProperty, value);
    }
}
