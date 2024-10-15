using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.WebRequests;
using DCL.WebRequests.ArgsFactory;
using ECS.StreamableLoading.Cache;

namespace ECS.StreamableLoading.Textures
{
    /// <summary>
    ///     We need a separate class to override the UpdateInGroup attribute
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadGlobalTextureSystem : LoadTextureSystem
    {
        internal LoadGlobalTextureSystem(World world, IStreamableCache<Texture2DData, GetTextureIntention> cache, IWebRequestController webRequestController, IGetTextureArgsFactory getTextureArgsFactory) : base(
            world, cache, webRequestController, getTextureArgsFactory
        ) { }

        public override void Dispose()
        {
            base.Dispose();
            //TODO move to the corresponding dispose point
            getTextureArgsFactory.Dispose();
        }
    }
}
