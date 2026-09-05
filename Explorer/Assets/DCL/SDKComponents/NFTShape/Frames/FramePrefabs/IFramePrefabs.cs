using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ECSComponents;
using DCL.SDKComponents.NFTShape.Frames.Pool;
using System.Collections.Generic;
using System.Threading;

namespace DCL.SDKComponents.NFTShape.Frames.FramePrefabs
{
    public interface IFramePrefabs : IReadOnlyFramePrefabs
    {
        UniTask InitializeAsync(
            IReadOnlyDictionary<NftFrameType, NFTShapeSettings.FrameRef> refs,
            NFTShapeSettings.FrameRef defaultRef,
            CancellationToken ct
        );
    }
}
