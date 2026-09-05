using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.ParticleSystem.Components;
using ECS.Abstract;
using ECS.LifeCycle;
using UnityEngine;

namespace DCL.SDKComponents.ParticleSystem.Systems
{
    [UpdateInGroup(typeof(ParticleSystemGroup))]
    [UpdateAfter(typeof(ParticleSystemApplyPropertiesSystem))]
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
                StopAllParticleSystemsQuery(World);
        }

        [Query]
        private void StopAllParticleSystems(ref ParticleSystemComponent component)
        {
            if (component.ParticleSystemInstance.isPlaying)
                component.ParticleSystemInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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
