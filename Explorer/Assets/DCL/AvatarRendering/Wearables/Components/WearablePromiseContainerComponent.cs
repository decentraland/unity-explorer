using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common;

namespace DCL.AvatarRendering.Wearables.Components
{
    public struct WearablePromiseContainerComponent
    {
        //TODO: How can I make both intentions inherent from the same interface to avoid the duplication?
        //Probably this component is not needed at all
        public AssetPromise<WearableDTO[], GetWearableByParamIntention> WearableRequestPromise;
        public AssetPromise<WearableDTO[], GetWearableByPointersIntention> WearableByPointerRequestPromise;
    }
}
