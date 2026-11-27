namespace SaffronWok.Mobile;

internal static class DishPhoto
{
    public static ImageSource? FromUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? ImageSource.FromUri(uri)
            : null;
}
