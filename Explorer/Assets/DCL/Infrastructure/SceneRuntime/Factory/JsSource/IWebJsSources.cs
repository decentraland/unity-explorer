using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Utility.Types;
using ECS.StreamableLoading.Cache.Disk;
using System.Threading;

namespace SceneRuntime.Factory.WebSceneSource
{
    public interface IWebJsSources
    {
        public UniTask<Result<SlicedOwnedMemory<byte>>> SceneSourceCodeAsync(URLAddress path,
            CancellationToken ct);
    }
}
