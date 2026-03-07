using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using UnityEngine;

namespace DCL.SDKComponents.ParticleSystem.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ParticleSystemApplyPropertiesSystem))]
    [ThrottlingEnabled]
    [LogCategory(ReportCategory.PARTICLE_SYSTEM)]
    public partial class ParticleSystemPlaybackSystem : BaseUnityLoopSystem
    {
        internal ParticleSystemPlaybackSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdatePlaybackQuery(World);
        }

        [Query]
        private void UpdatePlayback(ref PBParticleSystem pb, ref ParticleSystemComponent component)
        {
            var ps = component.ParticleSystemInstance;

            // Handle restart: any increment to restart_count triggers stop+clear+play
            if (pb.RestartCount > component.LastRestartCount)
            {
                component.LastRestartCount = pb.RestartCount;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ps.Play();
                return;
            }

            if (!pb.IsDirty) return;

            var state = pb.HasPlaybackState ? pb.PlaybackState : PBParticleSystem.Types.PlaybackState.PsPlaying;

            switch (state)
            {
                case PBParticleSystem.Types.PlaybackState.PsPlaying:
                    if (!ps.isPlaying) ps.Play();
                    break;

                case PBParticleSystem.Types.PlaybackState.PsPaused:
                    if (!ps.isPaused) ps.Pause();
                    break;

                case PBParticleSystem.Types.PlaybackState.PsStopped:
                    if (ps.isPlaying || ps.isPaused)
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    break;
            }
        }
    }
}
