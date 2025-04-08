using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace ECS.StreamableLoading.Textures
{
    public interface IProfileTextureUrlProvider
    {
        UniTask<URLAddress?> GetAsync(string userId, CancellationToken ct);
    }
}
