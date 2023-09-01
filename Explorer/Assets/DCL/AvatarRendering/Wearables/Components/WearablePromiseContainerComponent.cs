using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common;

namespace DCL.AvatarRendering.Wearables.Components
{
    public struct WearablePromiseContainerComponent
    {
        public AssetPromise<WearableDTO[], GetWearableByParamIntention> WearableRequestPromise;

        public AssetPromise<WearableDTO[], GetWearableByPointersIntention> WearableByPointerRequestPromise;

        public bool Done;
    }
}
