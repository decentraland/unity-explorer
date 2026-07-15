using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Threading;

namespace DCL.MarketplaceCredits.Purchase
{
    public class MarketplaceShopAPIClient
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private string marketplaceServerBaseUrl => decentralandUrlsSource.Url(DecentralandUrl.MarketplaceServer);

        public MarketplaceShopAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask<ShopListingDto?> GetShopListingForItemAsync(string contractAddress, string itemId, CancellationToken ct)
        {
            var url = $"{marketplaceServerBaseUrl}/v3/catalog/shop?contractAddress={contractAddress}&itemId={itemId}&first=1";

            ShopListingsResponse response = await webRequestController.GetAsync(new CommonArguments(URLAddress.FromString(url)), ct, ReportCategory.CREDITS_PURCHASE)
                                                                      .CreateFromJson<ShopListingsResponse>(WRJsonParser.Newtonsoft);

            return response.data is { Length: > 0 } ? response.data[0] : null;
        }

        public async UniTask<TradeDto?> GetTradeAsync(string tradeId, CancellationToken ct)
        {
            var url = $"{marketplaceServerBaseUrl}/v1/trades/{tradeId}";

            TradeResponse response = await webRequestController.GetAsync(new CommonArguments(URLAddress.FromString(url)), ct, ReportCategory.CREDITS_PURCHASE)
                                                               .CreateFromJson<TradeResponse>(WRJsonParser.Newtonsoft);

            return response.data;
        }
    }
}
