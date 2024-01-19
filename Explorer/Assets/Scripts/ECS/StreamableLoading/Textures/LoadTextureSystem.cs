using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Multithreading;

namespace ECS.StreamableLoading.Textures
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadTextureSystem : LoadSystemBase<Texture2D, GetTextureIntention>
    {
        private readonly IWebRequestController webRequestController;

        internal LoadTextureSystem(World world, IStreamableCache<Texture2D, GetTextureIntention> cache, IWebRequestController webRequestController, MutexSync mutexSync) : base(world, cache, mutexSync)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2D>> FlowInternalAsync(GetTextureIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            if (intention.IsVideoTexture)
                return new StreamableLoadingResult<Texture2D>(CreateVideoTexture(intention.WrapMode, intention.FilterMode));

            // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
            GetTextureWebRequest request = await webRequestController.GetTextureAsync(
                intention.CommonArguments,
                new GetTextureArguments(intention.IsReadable),
                ct,
                reportCategory: ReportCategory.TEXTURES);

            return new StreamableLoadingResult<Texture2D>(request.CreateTexture(intention.WrapMode, intention.FilterMode));
        }

        private static Texture2D CreateVideoTexture(TextureWrapMode wrapMode, FilterMode filterMode = FilterMode.Point)
        {
            var tex = new Texture2D(1, 1, TextureFormat.BGRA32, false, false)
            {
                wrapMode = wrapMode,
                filterMode = filterMode,
            };

            ProfilingCounters.TexturesAmount.Value++;
            tex.SetDebugName($"VideoTexture {ProfilingCounters.TexturesAmount}");

            return tex;
        }
    }
}
