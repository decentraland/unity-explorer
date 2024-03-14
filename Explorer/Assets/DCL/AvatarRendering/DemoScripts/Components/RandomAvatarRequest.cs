using System.Collections.Generic;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common;

namespace DCL.AvatarRendering.DemoScripts.Components
{
    public class RandomAvatarRequest
    {
        public List<AssetPromise<WearablesResponse, GetWearableByParamIntention>> CollectionPromise;
        public int RandomAvatarsToInstantiate;
        public bool IsSelfReplica = false;
    }
}