using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Profiles;
using DCL.WebRequests;
using ECS.Groups;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;

namespace ECS.StreamableLoading.Textures
{
    /// <summary>
    ///     We need a separate class to override the UpdateInGroup attribute
    /// </summary>
    [UpdateInGroup(typeof(LoadGlobalSystemGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadGlobalTextureSystem : LoadTextureSystem
    {
        internal LoadGlobalTextureSystem(World world, IStreamableCache<TextureData, GetTextureIntention> cache, IWebRequestController webRequestController, IDiskCache<TextureData> diskCache, IProfileRepository avatarTextureUrlProvider) : base(
            world, cache, webRequestController, diskCache, avatarTextureUrlProvider
        ) { }
    }
}
