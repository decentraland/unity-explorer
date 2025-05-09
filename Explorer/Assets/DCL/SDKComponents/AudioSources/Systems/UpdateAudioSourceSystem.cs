using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.Utilities.Extensions;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.Prioritization.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Audio;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AudioClips.AudioClipData, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace DCL.SDKComponents.AudioSources
{
    [UpdateInGroup(typeof(SDKAudioSourceGroup))]
    [UpdateAfter(typeof(StartAudioSourceLoadingSystem))]
    [LogCategory(ReportCategory.SDK_AUDIO_SOURCES)]
    public partial class UpdateAudioSourceSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
    {
        private readonly IPerformanceBudget frameTimeBudgetProvider;
        private readonly IPerformanceBudget memoryBudgetProvider;
        private readonly IComponentPool<AudioSource> audioSourcesPool;
        private readonly World world;
        private readonly ISceneData sceneData;
        private readonly AudioMixerGroup[]? worldGroup;
        private readonly ISceneStateProvider sceneStateProvider;

        internal UpdateAudioSourceSystem(World world, ISceneData sceneData, IComponentPoolsRegistry poolsRegistry,
            IPerformanceBudget frameTimeBudgetProvider,
            IPerformanceBudget memoryBudgetProvider, AudioMixer audioMixer, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.world = world;
            this.sceneData = sceneData;
            this.frameTimeBudgetProvider = frameTimeBudgetProvider;
            this.memoryBudgetProvider = memoryBudgetProvider;
            this.sceneStateProvider = sceneStateProvider;

            audioSourcesPool = poolsRegistry.GetReferenceTypePool<AudioSource>().EnsureNotNull();

            if (audioMixer != null)
                worldGroup = audioMixer.FindMatchingGroups("World");
        }

        protected override void Update(float t)
        {
            CreateAudioSourceQuery(World);
            UpdateAudioSourceQuery(World);
        }

        [Query]
        private void CreateAudioSource(ref PBAudioSource sdkAudioSource, ref AudioSourceComponent audioSourceComponent, ref TransformComponent entityTransform)
        {
            if (audioSourceComponent.ClipPromise.IsConsumed
                || NoBudget())
                return;

            if (!audioSourceComponent.ClipPromise.TryConsume(World!, out var promiseResult))
                return;

            if (audioSourceComponent.AudioSourceAssigned == false)
                audioSourceComponent.SetAudioSource(audioSourcesPool.Get()!, (worldGroup is { Length: > 0 } ? worldGroup[0] : null)!);

            AudioSource? audioSource = audioSourceComponent.AudioSource;

            if (audioSource != null)
            {
                audioSource.FromPBAudioSourceWithClip(sdkAudioSource, clip: promiseResult.Asset!);

                audioSource.Stop();

                if (audioSource.clip != null)
                    if (sdkAudioSource is {HasPlaying: true, Playing: true })
                        audioSource.Play();
            }

            // Reset isDirty as we just applied the PBAudioSource to the AudioSource
            sdkAudioSource.IsDirty = false;

            Transform transform = audioSource!.transform;
            transform.SetParent(entityTransform.Transform, false);
            transform.ResetLocalTRS();

            return;

            bool NoBudget() =>
                !frameTimeBudgetProvider.TrySpendBudget() || !memoryBudgetProvider.TrySpendBudget();
        }

        [Query]
        [All(typeof(PBAudioSource), typeof(AudioSourceComponent))]
        private void UpdateAudioSource(in Entity entity, ref PBAudioSource sdkComponent, ref AudioSourceComponent component, ref PartitionComponent partitionComponent)
        {
            HandleSDKChanges(entity, sdkComponent, ref component, partitionComponent);
        }


        public void OnSceneIsCurrentChanged(bool value)
        {
            UpdateCurrentSceneVolumeQuery(World, value);
        }

        [Query]
        private void UpdateCurrentSceneVolume([Data] bool isCurrentScene, ref PBAudioSource sdkComponent, ref AudioSourceComponent component)
        {
            if (component.AudioSourceAssigned)
            {
                if (isCurrentScene)
                {
                    component.Mute(false);
                    component.AudioSource!.volume = sdkComponent.GetVolume();
                }
                else
                    component.Mute(true);
            }
        }

        private void HandleSDKChanges(in Entity entity, PBAudioSource sdkComponent, ref AudioSourceComponent component, PartitionComponent partitionComponent)
        {
            if (!sceneStateProvider.IsCurrent)
                return;

            if (component.AudioSourceAssigned)
            {
                if ((sdkComponent.HasPlaying && sdkComponent.Playing) != component.AudioSource.isPlaying)
                {
                    if (sdkComponent.HasPlaying && sdkComponent.Playing)
                    {
                        Debug.Log($"JUANI WE ARE IN THE WEIRD STATE {entity.Id} NEEDS PLAY");
                        component.AudioSource.Play();
                    }
                    else
                    {
                        Debug.Log($"JUANI WE ARE IN THE WEIRD STATE {entity.Id} NEEDS STOP");
                        component.AudioSource.Stop();
                    }
                }
            }

            if (!sdkComponent.IsDirty) return;

            if (component.AudioSourceAssigned)
                component.AudioSource!.ApplyPBAudioSource(sdkComponent);

            // Don't play if the audio clip needs to change
            if (component.AudioClipUrl != sdkComponent.AudioClipUrl)
            {
                component.CleanUp(world);
                component.AudioClipUrl = sdkComponent.AudioClipUrl!;

                if (AudioUtils.TryCreateAudioClipPromise(world, sceneData, sdkComponent.AudioClipUrl!, partitionComponent, out Promise? clipPromise))
                    component.ClipPromise = clipPromise!.Value;
            }
            else
            {
                AudioSource? audioSource = component.AudioSource;

                if (audioSource?.clip != null)
                {
                    if (sdkComponent is {HasPlaying: true, Playing: true })
                    {
                        if (!audioSource.isPlaying)
                            audioSource.Play();
                    }
                    else
                        audioSource.Stop();
                }
            }

            sdkComponent.IsDirty = false;
        }

    }
}
