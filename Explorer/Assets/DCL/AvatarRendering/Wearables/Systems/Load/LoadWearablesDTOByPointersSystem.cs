using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.StreamableLoading.Cache;

namespace DCL.AvatarRendering.Wearables.Systems.Load
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadWearablesDTOByPointersSystem : LoadElementsByPointersSystem<WearablesDTOList, GetWearableDTOByPointersIntention, WearableDTO>
    {
        internal LoadWearablesDTOByPointersSystem(
            World world,
            IWebRequestController webRequestController,
            IStreamableCache<WearablesDTOList, GetWearableDTOByPointersIntention> cache
        ) : base(world, cache, webRequestController)
        {
        }

        protected override WearablesDTOList CreateAssetFromListOfDTOs(RepoolableList<WearableDTO> list) =>
            new (list);
    }
}
