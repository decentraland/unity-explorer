using DCL.ECSComponents;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Frames.Pool
{
    public interface IFramesPool
    {
        AbstractFrame NewFrame(NftFrameType frameType, Transform parent);

        void Release(AbstractFrame frame);
    }
}
