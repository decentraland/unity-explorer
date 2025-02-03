using System.Threading;
using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Common.Components;

namespace ECS.StreamableLoading.Textures
{
    /// <summary>
    ///     We need a separate class to override the UpdateInGroup attribute
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadGlobalTextureSystem : LoadTextureSystem
    {
        internal LoadGlobalTextureSystem(World world, IStreamableCache<Texture2DData, GetTextureIntention> cache, IWebRequestController webRequestController, IDiskCache<Texture2DData> diskCache) : base(
            world, cache, webRequestController, diskCache
        ) { }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> GetTextureAsync(GetTextureIntention intention, IPartitionComponent partition, CancellationToken ct)
        {
            //Global world always looks for the texture download
            var result = await TryTextureDownload(intention, ct);
            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(result.EnsureNotNull()));
        }
    }
}
