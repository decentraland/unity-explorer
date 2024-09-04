using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.StreamableLoading.Cache;

namespace DCL.AvatarRendering.Emotes.Load
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.EMOTE)]
    public partial class LoadEmotesByPointersSystem : LoadElementsByPointersSystem<EmotesDTOList, GetEmotesByPointersFromRealmIntention, EmoteDTO>
    {
        public LoadEmotesByPointersSystem(
            World world,
            IWebRequestController webRequestController,
            IStreamableCache<EmotesDTOList, GetEmotesByPointersFromRealmIntention> cache
        )
            : base(world, cache, webRequestController) { }

        protected override EmotesDTOList CreateAssetFromListOfDTOs(RepoolableList<EmoteDTO> list) =>
            new (list);
    }
}
