using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.StreamableLoading.Textures
{
    /// <summary>
    ///     We need a separate class to override the UpdateInGroup attribute
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadGlobalTextureSystem : LoadTextureSystem
    {
        internal LoadGlobalTextureSystem(World world, IStreamableCache<Texture2D, GetTextureIntention> cache, IWebRequestController webRequestController) : base(world, cache,webRequestController) { }
    }
}
