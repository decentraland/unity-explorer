using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.NftInfoAPIService
{
    public interface INftMarketAPIClient
    {
        UniTask<NftInfo> FetchNftInfoAsync(string contractAddress, string tokenId, CancellationToken ct);
    }
}
