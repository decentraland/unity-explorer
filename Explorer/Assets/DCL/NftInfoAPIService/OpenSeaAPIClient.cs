using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.WebRequests;
using System.Threading;

namespace DCL.NftInfoAPIService
{
    public class OpenSeaAPIClient : INftMarketAPIClient
    {
        private const string MARKET_NAME = "OpenSea";
        private const string BASE_URL = "https://opensea.decentraland.org";
        private const string CHAIN = "ethereum";

        private readonly IWebRequestController webRequestController;

        public OpenSeaAPIClient(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public async UniTask<NftInfo> FetchNftInfoAsync(string contractAddress, string tokenId, CancellationToken ct)
        {
            var url = $"{BASE_URL}/api/v2/chain/{CHAIN}/contract/{contractAddress}/nfts/{tokenId}";

            OpenSeaNftResponse nftResponse = await webRequestController.GetAsync(url, ct, reportCategory: ReportCategory.NFT_INFO_WEB_REQUEST)
               .CreateFromJson<OpenSeaNftResponse>(WRJsonParser.Unity, WRThreadFlags.SwitchToThreadPool);

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
