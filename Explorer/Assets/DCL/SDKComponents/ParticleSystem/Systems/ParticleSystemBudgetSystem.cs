using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.ParticleSystem.Components;
using ECS.Abstract;
using UnityEngine;

namespace DCL.SDKComponents.ParticleSystem.Systems
{
    [UpdateInGroup(typeof(ParticleSystemGroup))]
    [UpdateAfter(typeof(ParticleSystemPlaybackSystem))]
    [LogCategory(ReportCategory.PARTICLE_SYSTEM)]
    public partial class ParticleSystemBudgetSystem : BaseUnityLoopSystem
    {
        private const float EMISION_MULTIPLIER_EXTRA_THRESHOLD = 0.2f;

        private readonly ParticleSystemPlugin.ParticleSystemPluginSettings settings;
        private readonly ElementBinding<string> particleCountBinding;
        private readonly DebugWidgetVisibilityBinding visibilityBinding;
        private int totalParticles;

        internal ParticleSystemBudgetSystem(World world, ParticleSystemPlugin.ParticleSystemPluginSettings settings,
            ElementBinding<string> particleCountBinding, DebugWidgetVisibilityBinding visibilityBinding) : base(world)
        {
            this.settings = settings;
            this.particleCountBinding = particleCountBinding;
            this.visibilityBinding = visibilityBinding;
        }

        protected override void Update(float t)
        {
            int maxSceneParticles = settings.MaxSceneParticles;
            totalParticles = 0;
            CountParticlesQuery(World);

            float multiplier = totalParticles <= maxSceneParticles
                ? 1f
                : Mathf.Max(0f, ((float)maxSceneParticles / totalParticles) - EMISION_MULTIPLIER_EXTRA_THRESHOLD);

            ApplyBudgetQuery(World, multiplier);

            // Debug container Widget updater
            if (visibilityBinding.IsConnectedAndExpanded)
            {
                string color = totalParticles >= maxSceneParticles ? "red" : "green";
                particleCountBinding.Value = $"<color={color}>{totalParticles} / {maxSceneParticles}</color>";
            }
        }

        [Query]
        private void CountParticles(ref ParticleSystemComponent component)
        {
            totalParticles += component.ParticleSystemInstance.particleCount;
        }

        [Query]
        private void ApplyBudget([Data] float multiplier, ref ParticleSystemComponent component, in PBParticleSystem pbParticleSystem)
        {
            var emission = component.ParticleSystemInstance.emission;

            // Unity misnaming: rateOverTimeMultiplier is not a normalized multiplier, it holds the same value as the emission rate over time
            emission.rateOverTimeMultiplier = multiplier * pbParticleSystem.GetRate();
        }
    }
}
