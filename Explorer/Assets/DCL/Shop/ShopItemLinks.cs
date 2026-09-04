using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Passport.Modules;

namespace DCL.Shop
{
    public static class ShopItemLinks
    {
        private const int AMOY_CHAIN_ID = 80002;
        private const string NETWORK_AMOY = "amoy";
        private const string NETWORK_MATIC = "matic";

        public static string BuildItemUrl(IDecentralandUrlsSource urlsSource, string contractAddress, string itemId) =>
            $"{urlsSource.Url(DecentralandUrl.ShopLink)}/item/{contractAddress}/{itemId}?utm_source=client";

        public static string BuildItemUrn(int chainId, string contractAddress, string itemId) =>
            $"urn:decentraland:{(chainId == AMOY_CHAIN_ID ? NETWORK_AMOY : NETWORK_MATIC)}:collections-v2:{contractAddress.ToLowerInvariant()}:{itemId}";

        public static string BuildItemUrlFromUrn(IDecentralandUrlsSource urlsSource, string urn) =>
            CreditPurchaseBuyHandler.TryParseCollectionItem(urn, out string contractAddress, out string itemId)
                ? BuildItemUrl(urlsSource, contractAddress, itemId)
                : string.Empty;
    }
}
