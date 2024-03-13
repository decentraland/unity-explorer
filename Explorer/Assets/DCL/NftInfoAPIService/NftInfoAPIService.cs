using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.NftInfoAPIService
{
    public class NftInfoAPIService : INftInfoAPIService
    {
        private readonly INftMarketAPIClient nftMarketAPIClient;

        public NftInfoAPIService(INftMarketAPIClient nftMarketAPIClient)
        {
            this.nftMarketAPIClient = nftMarketAPIClient;
        }

        public UniTask<NftInfo> FetchNftInfoAsync(string contractAddress, string tokenId, CancellationToken ct) =>
            nftMarketAPIClient.FetchNftInfoAsync(contractAddress, tokenId, ct);
    }
}
