using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.AudioClips;
using UnityEngine;

namespace DCL.SDKComponents.AudioSources
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.SDK_AUDIO_ANALYSIS)]
    [ThrottlingEnabled]
    public partial class CleanUpAudioAnalysisSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private CleanUpAudioAnalysisSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleComponentRemovalQuery(World);
        }

        [Query]
        [None(typeof(PBAudioAnalysis), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref AudioSourceComponent component)
        {
            component.EnsureLastAudioFrameReadFilterIsRemoved();
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref AudioSourceComponent component)
        {
            component.EnsureLastAudioFrameReadFilterIsRemoved();
        }

        [Query]
        private void FinalizeComponents(ref AudioSourceComponent component)
        {
            component.EnsureLastAudioFrameReadFilterIsRemoved();
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }
    }
}
