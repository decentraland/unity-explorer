using System;

// ReSharper disable InconsistentNaming
namespace DCL.MarketplaceCredits.Purchase
{
    // Server schema: marketplace-server GET /v1/lists/:id/picks (controllers/handlers/favorites/lists-handlers.ts getPicksByListIdHandler),
    // mirrored by Server/shop/app/src/lib/favorites.ts Envelope<PicksPage>. `ok: false` can arrive on an HTTP 200.
    [Serializable]
    public class FavoritePicksResponse
    {
        public bool ok;
        public string? message;
        public FavoritePicksPageDto? data;
    }

    [Serializable]
    public class FavoritePicksPageDto
    {
        public FavoritePickDto[] results = null!;
        public int total;
    }

    // Server schema: marketplace-server adapters/lists/lists.ts fromDBGetPickByListIdToPickIdsWithCount.
    [Serializable]
    public class FavoritePickDto
    {
        public string itemId = null!;
        public long createdAt;
    }

    // Server schema: marketplace-server POST /v1/picks/:itemId (controllers/handlers/favorites/picks-handlers.ts pickAndUnpickInBulkHandler).
    [Serializable]
    public class FavoriteToggleResponse
    {
        public bool ok;
        public string? message;
    }
}
