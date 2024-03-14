using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common;

namespace DCL.AvatarRendering.DemoScripts.Components
{
    public class RandomAvatarRequest
    {
        public AssetPromise<WearablesResponse, GetWearableByParamIntention> BaseWearablesPromise;
        public int RandomAvatarsToInstantiate;
        public bool IsSelfReplica = false;
    }
}
