using CromoBound.Contracts;

namespace CromoBound.Client.Services;

/// <summary>The card catalog from the server (<c>GET /api/cards</c>), loaded the first time something needs it and kept for the tab. A
/// failed load isn't kept, so the next call tries again.</summary>
public sealed class CatalogClient(IServerApi api)
{
    private CardCatalog? _catalog;

    public async Task<ApiResult<CardCatalog>> GetAsync()
    {
        if (_catalog is not null) return new ApiResult<CardCatalog>(_catalog, null);
        var loaded = await api.CardsAsync();
        if (loaded.Value is { } catalog) _catalog = catalog;
        return loaded;
    }
}
