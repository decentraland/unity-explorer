using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.Threading;

namespace DCL.NftInfoAPIService
{
    public class OpenSeaAPIClient : INftMarketAPIClient
    {
        private const string MARKET_NAME = "OpenSea";
        private const string DEFAULT_CHAIN = "ethereum";

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        private Uri baseURL => decentralandUrlsSource.Url(DecentralandUrl.OpenSea);

        public OpenSeaAPIClient(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
        {
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public async UniTask<NftInfo> FetchNftInfoAsync(string chain, string contractAddress, string tokenId, CancellationToken ct)
        {
            Uri url = baseURL.Append($"/api/v2/chain/{(string.IsNullOrEmpty(chain) ? DEFAULT_CHAIN : chain)}/contract/{contractAddress}/nfts/{tokenId}");

            OpenSeaNftResponse nftResponse = await webRequestController.GetAsync(url, ReportCategory.NFT_INFO_WEB_REQUEST)
                                                                       .CreateFromJsonAsync<OpenSeaNftResponse>(WRJsonParser.Unity, ct, WRThreadFlags.SwitchToThreadPool);

            return ResponseToNftInfo(nftResponse.nft);
        }

        private NftInfo ResponseToNftInfo(OpenSeaNftData nft)
        {
            NftInfo ret = new NftInfo
            {
                marketName = MARKET_NAME,
                name = nft.name,
                description = nft.description,
                imageUrl = nft.image_url,
                assetLink = nft.opensea_url,
                marketLink = nft.opensea_url,
                tokenId = nft.identifier,
                owners = nft.owners,
                assetContract = new AssetContract
                {
                    address = nft.contract,
                    name = nft.contract, // we have no name now
                },
            };

            return ret;
        }
    }
}
