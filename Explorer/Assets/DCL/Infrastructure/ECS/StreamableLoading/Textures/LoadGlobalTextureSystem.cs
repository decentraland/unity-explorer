using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using DCL.WebRequests;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using UnityEngine;

namespace ECS.StreamableLoading.Textures
{
    /// <summary>
    ///     We need a separate class to override the UpdateInGroup attribute
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadGlobalTextureSystem : LoadTextureSystem
    {
        internal LoadGlobalTextureSystem(World world, IStreamableCache<Texture2DData, GetTextureIntention> cache, IWebRequestController webRequestController, IDiskCache<Texture2DData> diskCache, IAvatarTextureUrlProvider avatarTextureUrlProvider, IDecentralandUrlsSource urlsSource, ExtendedObjectPool<Texture2D> videoTexturePool) : base(
            world, cache, webRequestController, diskCache, avatarTextureUrlProvider, urlsSource, videoTexturePool
        ) { }
    }
}
