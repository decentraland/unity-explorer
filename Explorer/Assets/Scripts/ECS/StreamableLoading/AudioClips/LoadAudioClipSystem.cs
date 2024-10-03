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
    [LogCategory(ReportCategory.SDK_AUDIO_SOURCES)]
    public partial class LoadAudioClipSystem : LoadSystemBase<AudioClipData, GetAudioClipIntention>
    {
        private readonly IWebRequestController webRequestController;

        internal LoadAudioClipSystem(World world, IStreamableCache<AudioClipData, GetAudioClipIntention> cache, IWebRequestController webRequestController) : base(world, cache)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<AudioClipData>> FlowInternalAsync(GetAudioClipIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            // Attempts should be always 1 as there is a repeat loop in `LoadSystemBase`
            AudioClip? result = await webRequestController.GetAudioClipAsync(
                intention.CommonArguments,
                new GetAudioClipArguments(intention.AudioType),
                new GetAudioClipWebRequest.CreateAudioClipOp(),
                ct,
                GetReportData());

            return new StreamableLoadingResult<AudioClipData>(new AudioClipData(result));
        }
    }
}
