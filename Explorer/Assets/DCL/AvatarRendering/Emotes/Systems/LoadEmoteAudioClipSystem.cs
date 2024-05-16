using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System.Threading;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.AvatarRendering.Emotes
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.AUDIO_SOURCES)]
    public partial class LoadEmoteAudioClipSystem : LoadSystemBase<AudioClip, GetAudioClipIntention>
    {
        private readonly IWebRequestController webRequestController;

        internal LoadEmoteAudioClipSystem(World world, IStreamableCache<AudioClip, GetAudioClipIntention> cache, IWebRequestController webRequestController) : base(world, cache)
        {
            this.webRequestController = webRequestController;
        }

        protected override async UniTask<StreamableLoadingResult<AudioClip>> FlowInternalAsync(GetAudioClipIntention intention, IAcquiredBudget acquiredBudget, IPartitionComponent partition, CancellationToken ct)
        {
            AudioClip? result = await webRequestController.GetAudioClipAsync(
                intention.CommonArguments,
                new GetAudioClipArguments(intention.AudioType),
                new GetAudioClipWebRequest.CreateAudioClipOp(),
                ct,
                reportCategory: GetReportCategory());

            return new StreamableLoadingResult<AudioClip>(result);
        }
    }
}
