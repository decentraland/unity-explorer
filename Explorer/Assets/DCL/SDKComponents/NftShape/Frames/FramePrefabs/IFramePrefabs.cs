using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ECSComponents;
using DCL.SDKComponents.NftShape.Frames.Pool;
using System.Collections.Generic;
using System.Threading;

namespace DCL.SDKComponents.NftShape.Frames.FramePrefabs
{
    public interface IFramePrefabs : IReadOnlyFramePrefabs
    {
        UniTask Initialize(
            IReadOnlyDictionary<NftFrameType, NftShapeSettings.FrameRef> refs,
            NftShapeSettings.FrameRef defaultRef,
            CancellationToken ct
        );
    }
}
