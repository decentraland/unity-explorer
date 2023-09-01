using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct RandomAvatarConstructorComponent
    {
        public AssetPromise<WearableDTO[], GetWearableByParamIntention> WearableRequestPromise;
        public bool Done;
    }
}
