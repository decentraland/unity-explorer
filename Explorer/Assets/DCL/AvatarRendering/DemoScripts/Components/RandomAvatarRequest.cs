using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using ECS.StreamableLoading.Common;

namespace DCL.AvatarRendering.DemoScripts.Components
{
    public class RandomAvatarRequest
    {
        public AssetPromise<IWearable[], GetWearableByParamIntention> BaseWearablesPromise;
        public int RandomAvatarsToInstantiate;
    }
}
