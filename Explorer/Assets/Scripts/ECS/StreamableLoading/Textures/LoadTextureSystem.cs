using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Utilities.Extensions;
using DCL.WebRequests.ArgsFactory;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.Unity.Textures.Utils;
using System;
using System.Threading;

namespace ECS.StreamableLoading.Textures
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.TEXTURES)]
    public partial class LoadTextureSystem : LoadSystemBase<Texture2DData, GetTextureIntention>
    {
        private readonly IWebRequestController webRequestController;
        protected readonly IGetTextureArgsFactory getTextureArgsFactory;

        internal LoadTextureSystem(World world, IStreamableCache<Texture2DData, GetTextureIntention> cache, IWebRequestController webRequestController, IGetTextureArgsFactory getTextureArgsFactory) : base(world, cache)
        {
            this.webRequestController = webRequestController;
            this.getTextureArgsFactory = getTextureArgsFactory;
        }

        protected override async UniTask<StreamableLoadingResult<Texture2DData>> FlowInternalAsync(GetTextureIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            if (intention.IsVideoTexture) throw new NotSupportedException($"{nameof(LoadTextureSystem)} does not support video textures. They should be handled by {nameof(VideoTextureUtils)}");

            // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
            var result = await webRequestController.GetTextureAsync(
                intention.CommonArguments,
                getTextureArgsFactory.NewArguments(intention.TextureType),
                GetTextureWebRequest.CreateTexture(intention.WrapMode, intention.FilterMode),
                ct,
                GetReportData()
            );

            return new StreamableLoadingResult<Texture2DData>(new Texture2DData(result.EnsureNotNull()));
        }
    }
}
