using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.StreamableLoading.NftShapes
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.NFT_SHAPE_WEB_REQUEST)]
    public partial class LoadNftShapeSystem : LoadSystemBase<Texture2D, GetNftShapeIntention>
    {
        private readonly IWebRequestController webRequestController;

        internal LoadNftShapeSystem(World world, IStreamableCache<Texture2D, GetNftShapeIntention> cache, IWebRequestController webRequestController, MutexSync mutexSync) : base(world, cache, mutexSync)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2D>> FlowInternalAsync(GetNftShapeIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            //head request

            //texture request
            // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
            GetTextureWebRequest request = await webRequestController.GetTextureAsync(
                intention.CommonArguments,
                new GetTextureArguments(false),
                ct,
                reportCategory: GetReportCategory()
            );

            return new StreamableLoadingResult<Texture2D>(request.CreateTexture(TextureWrapMode.Clamp, FilterMode.Bilinear)!);
        }
    }
}
