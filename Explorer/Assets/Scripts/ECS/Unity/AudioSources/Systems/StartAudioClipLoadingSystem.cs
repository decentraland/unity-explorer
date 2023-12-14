using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.AudioSources.Components;
using SceneRunner.Scene;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.AudioClip, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace ECS.Unity.AudioSources.Systems
{
    /// <summary>
    ///     Places a loading intention that can be consumed by other systems in the pipeline.
    ///     Does not provide support for Video Textures
    /// </summary>
    [UpdateInGroup(typeof(AudioSourceLoadingGroup))]
    [ThrottlingEnabled]
    public partial class StartAudioClipLoadingSystem : BaseUnityLoopSystem
    {
        private readonly ISceneData sceneData;
        private readonly int attemptsCount;
        private readonly IConcurrentBudgetProvider frameTimeBudgetProvider;

        internal StartAudioClipLoadingSystem(World world, ISceneData sceneData, int attemptsCount, IConcurrentBudgetProvider frameTimeBudgetProvider) : base(world)
        {
            this.sceneData = sceneData;
            this.attemptsCount = attemptsCount;
            this.frameTimeBudgetProvider = frameTimeBudgetProvider;
        }

        protected override void Update(float t)
        {
            CreateAudioSourceComponentWithPromiseQuery(World);
        }

        [Query]
        [All(typeof(PBAudioSource))]
        [None(typeof(AudioSourceComponent))]
        private void CreateAudioSourceComponentWithPromise(in Entity entity, ref PBAudioSource sdkAudioSource, ref PartitionComponent partitionComponent)
        {
            if (!frameTimeBudgetProvider.TrySpendBudget()) return;
            if (!sceneData.TryGetContentUrl(sdkAudioSource.AudioClipUrl, out URLAddress audioClipUrl)) return;

            var audioSourceComponent = new AudioSourceComponent(sdkAudioSource, sceneData);

            audioSourceComponent.ClipPromise = Promise.Create(World, new GetAudioClipIntention
            {
                CommonArguments = new CommonLoadingArguments(audioClipUrl, attempts: attemptsCount),
                AudioType = sdkAudioSource.AudioClipUrl.ToAudioType(),
            }, partitionComponent);

            audioSourceComponent.ClipLoadingStatus = StreamableLoading.LifeCycle.LoadingInProgress;
            World.Add(entity, audioSourceComponent);
        }
    }
}
