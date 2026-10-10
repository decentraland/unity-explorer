using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.MediaStream;
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
            HandleEntityDestructionAudioQuery(World);
            HandleComponentRemovalAudioQuery(World);

            HandleEntityDestructionPlayerQuery(World);
            HandleComponentRemovalPlayerQuery(World);
        }

#region AudioSourceComponent
        [Query]
        [None(typeof(PBAudioAnalysis), typeof(DeleteEntityIntention))]
        private void HandleComponentRemovalAudio(ref AudioSourceComponent component)
        {
            component.EnsureLastAudioFrameReadFilterIsRemoved();
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestructionAudio(ref AudioSourceComponent component)
        {
            component.EnsureLastAudioFrameReadFilterIsRemoved();
        }

        [Query]
        private void FinalizeComponentsAudio(ref AudioSourceComponent component)
        {
            component.EnsureLastAudioFrameReadFilterIsRemoved();
        }
#endregion


#region MediaPlayerComponent
        [Query]
        [None(typeof(PBAudioAnalysis), typeof(DeleteEntityIntention))]
        private void HandleComponentRemovalPlayer(ref MediaPlayerComponent component)
        {
            component.EnsureLastAudioFrameReadFilterIsRemoved();
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestructionPlayer(ref MediaPlayerComponent component)
        {
            component.EnsureLastAudioFrameReadFilterIsRemoved();
        }

        [Query]
        private void FinalizeComponentsPlayer(ref MediaPlayerComponent component)
        {
            component.EnsureLastAudioFrameReadFilterIsRemoved();
        }
#endregion

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsAudioQuery(World);
            FinalizeComponentsPlayerQuery(World);
        }
    }
}
