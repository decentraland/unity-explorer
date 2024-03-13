using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.NftInfoAPIService
{
    public interface INftInfoAPIService
    {
        UniTask<NftInfo> FetchNftInfoAsync(string contractAddress, string tokenId, CancellationToken ct);
    }
}
