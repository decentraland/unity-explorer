using DCL.ECSComponents;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Frame
{
    public interface IFramesPool
    {
        GameObject NewFrame(NftFrameType frameType, Transform parent);

        void Release(GameObject frame);
    }
}
