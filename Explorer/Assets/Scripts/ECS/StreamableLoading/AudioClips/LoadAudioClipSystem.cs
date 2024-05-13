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

namespace ECS.StreamableLoading.AudioClips
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [LogCategory(ReportCategory.AUDIO_SOURCES)]
    public partial class LoadAudioClipSystem  : LoadSystemBase<AudioClip, GetAudioClipIntention>
    {
        private readonly IWebRequestController webRequestController;

        internal LoadAudioClipSystem(World world, IStreamableCache<AudioClip, GetAudioClipIntention> cache, IWebRequestController webRequestController) : base(world, cache)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<AudioClip>> FlowInternalAsync(GetAudioClipIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
            var result = await webRequestController.GetAudioClipAsync(
                intention.CommonArguments,
                new GetAudioClipArguments(intention.AudioType),
                new GetAudioClipWebRequest.CreateAudioClipOp(),
                ct,
                reportCategory: GetReportCategory());

            return new StreamableLoadingResult<AudioClip>(result);
        }
    }
}
