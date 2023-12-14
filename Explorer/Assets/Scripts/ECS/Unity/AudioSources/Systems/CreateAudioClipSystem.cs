using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.AudioSources.Components;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using Utility;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.AudioClip, ECS.StreamableLoading.AudioClips.GetAudioClipIntention>;

namespace ECS.Unity.AudioSources.Systems
{
    [UpdateInGroup(typeof(AudioSourceLoadingGroup))]
    [UpdateAfter(typeof(StartAudioClipLoadingSystem))]
    public partial class CreateAudioSourceSystem : BaseUnityLoopSystem
    {
        private readonly IConcurrentBudgetProvider frameTimeBudgetProvider;
        private readonly IConcurrentBudgetProvider memoryBudgetProvider;
        private readonly IComponentPool<AudioSource> audioSourcesPool;

        internal CreateAudioSourceSystem(World world, IComponentPoolsRegistry poolsRegistry, IConcurrentBudgetProvider frameTimeBudgetProvider, IConcurrentBudgetProvider memoryBudgetProvider) : base(world)
        {
            this.frameTimeBudgetProvider = frameTimeBudgetProvider;
            this.memoryBudgetProvider = memoryBudgetProvider;

            audioSourcesPool = poolsRegistry.GetReferenceTypePool<AudioSource>();
        }

        protected override void Update(float t)
        {
            CreateAudioSourceQuery(World);
        }

        [Query]
        private void CreateAudioSource(ref AudioSourceComponent audioSourceComponent, ref TransformComponent entityTransform)
        {
            if (!frameTimeBudgetProvider.TrySpendBudget() || !memoryBudgetProvider.TrySpendBudget()) return;
            if (audioSourceComponent.ClipLoadingStatus != StreamableLoading.LifeCycle.LoadingInProgress) return;

            if (TryGetAudioClipResult(ref audioSourceComponent.ClipPromise, out StreamableLoadingResult<AudioClip> promiseResult))
            {
                audioSourceComponent.ClipLoadingStatus = StreamableLoading.LifeCycle.LoadingFinished;

                if(audioSourceComponent.Result == null)
                    audioSourceComponent.Result = audioSourcesPool.Get();

                var sdkAudioSource = audioSourceComponent.PBAudioSource;
                var audioSource = audioSourceComponent.Result;

                audioSource.spatialize = false;
                audioSource.spatialBlend = 0; //1;
                // audioSource.dopplerLevel = 0.1f;
                audioSource.playOnAwake = true; // for testing purposes

                audioSource.loop = true; // sdkAudioSource.Loop;
                // audioSource.pitch = sdkAudioSource.Pitch;
                audioSource.volume = 1; //sdkAudioSource.Volume;
                audioSource.clip = promiseResult.Asset;

                // if (sdkAudioSource.Playing && audioSource.clip != null)
                if (audioSource.clip != null)
                    audioSource.Play();

                Transform rendererTransform = audioSource.transform;
                rendererTransform.SetParent(entityTransform.Transform, false);
                rendererTransform.ResetLocalTRS();
            }
        }

        private bool TryGetAudioClipResult(ref Promise? promise, out StreamableLoadingResult<AudioClip> result)
        {
            result = default(StreamableLoadingResult<AudioClip>);

            if (promise == null)
                return true;

            return promise.Value.TryGetResult(World, out result);
        }
    }
}
