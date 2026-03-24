using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.ParticleSystem.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using UnityEngine;

namespace DCL.SDKComponents.ParticleSystem.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ParticleSystemApplyPropertiesSystem))]
    [ThrottlingEnabled]
    [LogCategory(ReportCategory.PARTICLE_SYSTEM)]
    public partial class ParticleSystemPlaybackSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
    {
        internal ParticleSystemPlaybackSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdatePlaybackQuery(World);
        }

        [Query]
        private void UpdatePlayback(ref PBParticleSystem particleSystemData, ref ParticleSystemComponent component)
        {
            var particleSystem = component.ParticleSystemInstance;

            // Handle restart: any increment to restart_count triggers stop+clear+play
            if (particleSystemData.RestartCount > component.LastRestartCount)
            {
                component.LastRestartCount = particleSystemData.RestartCount;
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystem.Play();
                return;
            }

            if (!particleSystemData.IsDirty) return;

            var state = particleSystemData.GetPlaybackState();

            switch (state)
            {
                case PBParticleSystem.Types.PlaybackState.PsPlaying:
                    if (!particleSystem.isPlaying) particleSystem.Play();
                    break;

                case PBParticleSystem.Types.PlaybackState.PsPaused:
                    if (!particleSystem.isPaused) particleSystem.Pause();
                    break;

                case PBParticleSystem.Types.PlaybackState.PsStopped:
                    if (particleSystem.isPlaying || particleSystem.isPaused)
                        particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    break;
            }
        }

        public void OnSceneIsCurrentChanged(bool enteredScene)
        {
            if (enteredScene)
                ResumeAllParticleSystemsQuery(World);
            else
                PauseAllParticleSystemsQuery(World);
        }

        [Query]
        private void PauseAllParticleSystems(ref ParticleSystemComponent component)
        {
            if (component.ParticleSystemInstance.isPlaying)
                component.ParticleSystemInstance.Pause();
        }

        [Query]
        private void ResumeAllParticleSystems(ref PBParticleSystem particleSystemData, ref ParticleSystemComponent component)
        {
            var state = particleSystemData.GetPlaybackState();

            switch (state)
            {
                case PBParticleSystem.Types.PlaybackState.PsPlaying:
                    component.ParticleSystemInstance.Play();
                    break;
                case PBParticleSystem.Types.PlaybackState.PsPaused:
                    break;
                case PBParticleSystem.Types.PlaybackState.PsStopped:
                    break;
            }
        }
    }
}
