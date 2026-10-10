using Cysharp.Threading.Tasks;
using DCL.Web3;
using System.Threading;

namespace DCL.Profiles
{
    public partial interface INftNamesProvider
    {
        UniTask<PaginatedNamesResponse> GetAsync(Web3Address userId, int pageNumber, int pageSize, CancellationToken ct);
    }
}
