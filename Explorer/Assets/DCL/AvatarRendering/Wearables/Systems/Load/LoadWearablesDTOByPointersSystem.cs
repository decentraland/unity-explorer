using Arch.Core;
using Arch.SystemGroups;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.WebRequests;
using ECS.Groups;
using ECS.StreamableLoading.Cache;

namespace DCL.AvatarRendering.Wearables.Systems
{
    [UpdateInGroup(typeof(LoadGlobalSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadWearablesDTOByPointersSystem : LoadElementsByPointersSystem<WearablesDTOList, GetWearableDTOByPointersIntention, WearableDTO>
    {
        internal LoadWearablesDTOByPointersSystem(
            World world,
            IWebRequestController webRequestController,
            IStreamableCache<WearablesDTOList, GetWearableDTOByPointersIntention> cache,
            EntitiesAnalytics entitiesAnalytics,
            IDecentralandUrlsSource urlsSource
        ) : base(world, cache, webRequestController, entitiesAnalytics, urlsSource)
        {
        }

        protected override WearablesDTOList CreateAssetFromListOfDTOs(RepoolableList<WearableDTO> list) =>
            new (list);
    }
}
