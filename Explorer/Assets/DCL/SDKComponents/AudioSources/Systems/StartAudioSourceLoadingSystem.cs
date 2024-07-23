using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.Prioritization.Components;
using SceneRunner.Scene;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.AudioClip, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.SDKComponents.AudioSources
{
    /// <summary>
    ///     Places a loading intention for audio clip that can be consumed by other systems in the pipeline.
    /// </summary>
    [UpdateInGroup(typeof(SDKAudioSourceGroup))]
    [LogCategory(ReportCategory.SDK_AUDIO_SOURCES)]
    [ThrottlingEnabled]
    public partial class StartAudioSourceLoadingSystem : BaseUnityLoopSystem
    {
        private readonly ISceneData sceneData;
        private readonly IPerformanceBudget frameTimeBudgetProvider;

        internal StartAudioSourceLoadingSystem(World world, ISceneData sceneData, IPerformanceBudget frameTimeBudgetProvider) : base(world)
        {
            this.sceneData = sceneData;
            this.frameTimeBudgetProvider = frameTimeBudgetProvider;
        }

        protected override void Update(float t)
        {
            CreateAudioSourceComponentWithPromiseQuery(World);
        }

        [Query]
        [None(typeof(AudioSourceComponent))]
        private void CreateAudioSourceComponentWithPromise(in Entity entity, ref PBAudioSource sdkAudioSource, ref PartitionComponent partitionComponent)
        {
            if (!frameTimeBudgetProvider.TrySpendBudget()) return;
            if (!AudioUtils.TryCreateAudioClipPromise(World, sceneData, sdkAudioSource.AudioClipUrl, partitionComponent, out Promise? assetPromise)) return;

            var audioSourceComponent = new AudioSourceComponent(assetPromise!.Value, sdkAudioSource.AudioClipUrl);
            World.Add(entity, audioSourceComponent);
        }
    }
}
