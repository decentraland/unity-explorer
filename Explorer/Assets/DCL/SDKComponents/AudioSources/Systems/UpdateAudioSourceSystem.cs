using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.AudioSources
{
    [UpdateInGroup(typeof(SDKAudioSourceGroup))]
    [UpdateAfter(typeof(StartAudioSourceLoadingSystem))]
    [LogCategory(ReportCategory.AUDIO_SOURCES)]
    public partial class UpdateAudioSourceSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget frameTimeBudgetProvider;
        private readonly IPerformanceBudget memoryBudgetProvider;
        private readonly IComponentPool<AudioSource> audioSourcesPool;

        internal UpdateAudioSourceSystem(World world, IComponentPoolsRegistry poolsRegistry, IPerformanceBudget frameTimeBudgetProvider, IPerformanceBudget memoryBudgetProvider) : base(world)
        {
            this.frameTimeBudgetProvider = frameTimeBudgetProvider;
            this.memoryBudgetProvider = memoryBudgetProvider;

            audioSourcesPool = poolsRegistry.GetReferenceTypePool<AudioSource>();
        }

        protected override void Update(float t)
        {
            CreateAudioSourceQuery(World);
            UpdateAudioSourceQuery(World);
            // TODO: Handle Volume updates - refer to ECSAudioSourceComponentHandler.cs in unity-renderer and check UpdateAudioSourceVolume() method and its usages
        }

        [Query]
        [All(typeof(PBAudioSource), typeof(AudioSourceComponent))]
        private void UpdateAudioSource(ref PBAudioSource sdkAudioSource, ref AudioSourceComponent audioSourceComponent)
        {
            if (sdkAudioSource.IsDirty)
            {
                audioSourceComponent.Result.ApplyPBAudioSource(sdkAudioSource);
                sdkAudioSource.IsDirty = false;
            }

            // TODO: Handle clip url changes - refer to ECSAudioSourceComponentHandler.cs in unity-renderer
        }

        [Query]
        private void CreateAudioSource(ref AudioSourceComponent audioSourceComponent, ref TransformComponent entityTransform)
        {
            if (NoBudget() || audioSourceComponent.ClipPromise == null || !audioSourceComponent.ClipPromise.Value.TryConsume(World, out StreamableLoadingResult<AudioClip> promiseResult))
                return;

            if (audioSourceComponent.Result == null)
                audioSourceComponent.Result ??= audioSourcesPool.Get();

            audioSourceComponent.Result.FromPBAudioSource(promiseResult.Asset, audioSourceComponent.PBAudioSource);

            Transform transform = audioSourceComponent.Result.transform;
            transform.SetParent(entityTransform.Transform, false);
            transform.ResetLocalTRS();

            return;

            bool NoBudget() =>
                !frameTimeBudgetProvider.TrySpendBudget() || !memoryBudgetProvider.TrySpendBudget();
        }
    }
}
