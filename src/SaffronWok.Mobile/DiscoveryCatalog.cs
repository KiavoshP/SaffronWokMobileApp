using System.Net.Http.Json;
using SaffronWok.Shared;

namespace SaffronWok.Mobile;

internal sealed class DiscoveryCatalog(HttpClient api)
{
    private EventMenu? preloadedMenu;

    public HttpClient Api { get; } = api;
    public IReadOnlyList<ServiceEvent> Events { get; private set; } = [];
    public IReadOnlyList<DailySpecial> Specials { get; private set; } = [];

    public async Task<IReadOnlyList<ServiceEvent>> GetEventsAsync(bool refresh = false)
    {
        if (refresh || Events.Count == 0)
            await LoadAsync(CancellationToken.None);

        return Events;
    }

    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var data = await Api.GetFromJsonAsync<DiscoveryBootstrap>("api/bootstrap", cancellationToken);
        if (data is null)
            return;

        Events = data.Events;
        Specials = data.Specials;
        preloadedMenu = data.Menu;
    }

    public async Task<EventMenu?> GetMenuAsync(string eventId)
    {
        if (preloadedMenu is { } menu && menu.EventId == eventId)
        {
            preloadedMenu = null;
            return menu;
        }

        return await Api.GetFromJsonAsync<EventMenu>($"api/events/{Uri.EscapeDataString(eventId)}/menu");
    }
}
