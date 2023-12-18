using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.AudioSources
{
    [UpdateInGroup(typeof(AudioSourceLoadingGroup))]
    [UpdateAfter(typeof(StartAudioSourceLoadingSystem))]
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
            if (NoBudget() || audioSourceComponent.ClipIsNotLoading || audioSourceComponent.ClipPromise == null
                           || !audioSourceComponent.ClipPromise.Value.TryGetResult(World, out var promiseResult))
                return;

            audioSourceComponent.ClipLoadingStatus = ECS.StreamableLoading.LifeCycle.LoadingFinished;

            if (audioSourceComponent.Result == null)
                audioSourceComponent.Result ??= audioSourcesPool.Get();

            audioSourceComponent.Result.FromPBAudioSource(promiseResult.Asset, audioSourceComponent.PBAudioSource);

            Transform rendererTransform = audioSourceComponent.Result.transform;
            rendererTransform.SetParent(entityTransform.Transform, false);
            rendererTransform.ResetLocalTRS();

            return;

            bool NoBudget() =>
                !frameTimeBudgetProvider.TrySpendBudget() || !memoryBudgetProvider.TrySpendBudget();
        }
    }

    internal static class AudioSourceExtensions
    {
        internal static AudioSource FromPBAudioSource(this AudioSource audioSource, AudioClip clip, PBAudioSource pbAudioSource)
        {
            audioSource.clip = clip;

            audioSource.playOnAwake = false;
            audioSource.spatialize = true;
            audioSource.spatialBlend = 1;
            audioSource.dopplerLevel = 0.1f;

            audioSource.loop = pbAudioSource.Loop;
            audioSource.pitch = pbAudioSource.Pitch;
            audioSource.volume = pbAudioSource.Volume;

            if (pbAudioSource.Playing && audioSource.clip != null)
                audioSource.Play();

            return audioSource;
        }
    }
}
