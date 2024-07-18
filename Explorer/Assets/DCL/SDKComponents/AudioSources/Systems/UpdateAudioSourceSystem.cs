using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.AudioClips;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Audio;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.AudioClip, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.SDKComponents.AudioSources
{
    [UpdateInGroup(typeof(SDKAudioSourceGroup))]
    [UpdateAfter(typeof(StartAudioSourceLoadingSystem))]
    [LogCategory(ReportCategory.SDK_AUDIO_SOURCES)]
    public partial class UpdateAudioSourceSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget frameTimeBudgetProvider;
        private readonly IPerformanceBudget memoryBudgetProvider;
        private readonly IComponentPool<AudioSource> audioSourcesPool;
        private readonly World world;
        private readonly ISceneData sceneData;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IStreamableCache<AudioClip, GetAudioClipIntention> cache;
        private readonly AudioMixer audioMixerController;

        internal UpdateAudioSourceSystem(World world, ISceneData sceneData, ISceneStateProvider sceneStateProvider, IStreamableCache<AudioClip, GetAudioClipIntention> cache, IComponentPoolsRegistry poolsRegistry,
            IPerformanceBudget frameTimeBudgetProvider,
            IPerformanceBudget memoryBudgetProvider, AudioMixer audioMixerController) : base(world)
        {
            this.world = world;
            this.sceneData = sceneData;
            this.sceneStateProvider = sceneStateProvider;
            this.frameTimeBudgetProvider = frameTimeBudgetProvider;
            this.memoryBudgetProvider = memoryBudgetProvider;
            this.cache = cache;
            this.audioMixerController = audioMixerController;

            audioSourcesPool = poolsRegistry.GetReferenceTypePool<AudioSource>().EnsureNotNull();
        }

        protected override void Update(float t)
        {
            CreateAudioSourceQuery(World);
            UpdateAudioSourceQuery(World);
        }

        [Query]
        private void CreateAudioSource(ref PBAudioSource sdkAudioSource, ref AudioSourceComponent audioSourceComponent, ref TransformComponent entityTransform)
        {
            if (NoBudget()
                || audioSourceComponent.ClipPromise.IsConsumed
                || !audioSourceComponent.ClipPromise.TryConsume(World!, out StreamableLoadingResult<AudioClip> promiseResult))
                return;

            if (audioSourceComponent.AudioSourceAssigned == false)
            {
                AudioMixerGroup[]? worldMixerGroups = audioMixerController.FindMatchingGroups("World");
                if (worldMixerGroups is { Length: > 0 })
                    audioSourceComponent.SetAudioSource(audioSourcesPool.Get()!, worldMixerGroups[0]);
            }

            audioSourceComponent.AudioSource!.FromPBAudioSourceWithClip(sdkAudioSource, clip: promiseResult.Asset);

            // Reset isDirty as we just applied the PBAudioSource to the AudioSource
            sdkAudioSource.IsDirty = false;

            Transform transform = audioSourceComponent.AudioSource!.transform;
            transform.SetParent(entityTransform.Transform, false);
            transform.ResetLocalTRS();

            return;

            bool NoBudget() =>
                !frameTimeBudgetProvider.TrySpendBudget() || !memoryBudgetProvider.TrySpendBudget();
        }

        [Query]
        [All(typeof(PBAudioSource), typeof(AudioSourceComponent))]
        private void UpdateAudioSource(ref PBAudioSource sdkComponent, ref AudioSourceComponent component, ref PartitionComponent partitionComponent)
        {
            if (component.AudioSourceAssigned)
                component.AudioSource!.volume = sceneStateProvider.IsCurrent ? sdkComponent.GetVolume() : 0;

            HandleSDKChanges(sdkComponent, ref component, partitionComponent);
        }

        private void HandleSDKChanges(PBAudioSource sdkComponent, ref AudioSourceComponent component, PartitionComponent partitionComponent)
        {
            if (!sdkComponent.IsDirty) return;

            if (component.AudioSourceAssigned)
                component.AudioSource!.ApplyPBAudioSource(sdkComponent);

            if (component.AudioClipUrl != sdkComponent.AudioClipUrl)
            {
                component.CleanUp(world, cache);
                component.AudioClipUrl = sdkComponent.AudioClipUrl!;

                if (AudioUtils.TryCreateAudioClipPromise(world, sceneData, sdkComponent.AudioClipUrl!, partitionComponent, out Promise? clipPromise))
                    component.ClipPromise = clipPromise!.Value;
            }

            sdkComponent.IsDirty = false;
        }
    }
}
