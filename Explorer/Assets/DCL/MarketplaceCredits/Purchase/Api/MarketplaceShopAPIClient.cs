using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System.Threading;
using UnityEngine.Pool;

namespace DCL.MarketplaceCredits.Purchase
{
    public class MarketplaceShopAPIClient
    {
        private static readonly URLParameter LISTING_TYPE_PRIMARY = new ("listingType", "primary");

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private string marketplaceServerBaseUrl => decentralandUrlsSource.Url(DecentralandUrl.MarketplaceServer);

        public MarketplaceShopAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public virtual async UniTask<ShopListingDto?> GetShopListingForItemAsync(string contractAddress, string itemId, CancellationToken ct)
        {
            using PooledObject<URLBuilder> _ = decentralandUrlsSource.BuildFromDomain(DecentralandUrl.MarketplaceServer, out URLBuilder urlBuilder);

            urlBuilder.AppendPath(new URLPath("v3/catalog/unified"));
            urlBuilder.AppendParameter(new URLParameter("contractAddress", contractAddress));
            urlBuilder.AppendParameter(new URLParameter("itemId", itemId));
            urlBuilder.AppendParameter(new URLParameter("first", "1"));
            urlBuilder.AppendParameter(LISTING_TYPE_PRIMARY);

            ShopListingsResponse response = await webRequestController.GetAsync(new CommonArguments(urlBuilder.Build()), ct, ReportCategory.CREDITS_PURCHASE)
                                                                      .CreateFromJson<ShopListingsResponse>(WRJsonParser.Newtonsoft);

            return response.data is { Length: > 0 } ? response.data[0] : null;
        }

        public virtual async UniTask<TradeDto?> GetTradeAsync(string tradeId, CancellationToken ct)
        {
            var url = $"{marketplaceServerBaseUrl}/v1/trades/{tradeId}";

            TradeResponse response = await webRequestController.GetAsync(new CommonArguments(URLAddress.FromString(url)), ct, ReportCategory.CREDITS_PURCHASE)
                                                               .CreateFromJson<TradeResponse>(WRJsonParser.Newtonsoft);

            return response.data;
        }
    }
}
