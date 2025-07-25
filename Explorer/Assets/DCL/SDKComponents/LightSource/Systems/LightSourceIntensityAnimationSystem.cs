using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.LightSource.Systems
{
    /// <summary>
    /// Updates the properties of existing light sources.
    /// </summary>
    [UpdateInGroup(typeof(LightSourcesGroup))]
    [UpdateAfter(typeof(LightSourceLodSystem))]
    [LogCategory(ReportCategory.LIGHT_SOURCE)]
    public partial class LightSourceIntensityAnimationSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly LightSourceSettings settings;

        public LightSourceIntensityAnimationSystem(World world, ISceneStateProvider sceneStateProvider, LightSourceSettings settings) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.settings = settings;
        }

        protected override void Update(float t)
        {
            AnimateLightSourceIntensityQuery(World,  settings.FadeDuration > 0 ? Time.unscaledDeltaTime / settings.FadeDuration : 1);
        }

        [Query]
        private void AnimateLightSourceIntensity([Data] float dt, ref LightSourceComponent lightSourceComponent, in PBLightSource pbLightSource)
        {
            if (!LightSourceHelper.IsPBLightSourceActive(pbLightSource, settings.DefaultValues.Active)) return;

            bool isLightOn = sceneStateProvider.IsCurrent && !lightSourceComponent.IsCulled;
            lightSourceComponent.TargetIntensity = isLightOn ? lightSourceComponent.MaxIntensity : 0;

            float delta = dt * lightSourceComponent.MaxIntensity;
            lightSourceComponent.CurrentIntensity = Mathf.MoveTowards(lightSourceComponent.CurrentIntensity, lightSourceComponent.TargetIntensity, delta);

            Light lightSourceInstance = lightSourceComponent.LightSourceInstance;

            lightSourceInstance.intensity = lightSourceComponent.CurrentIntensity;

            bool shouldOverrideRange = pbLightSource.HasRange && pbLightSource.Range > 0;
            lightSourceInstance.range = shouldOverrideRange ? pbLightSource.Range : Mathf.Pow(lightSourceComponent.CurrentIntensity, settings.RangeFormulaExponent);

            lightSourceInstance.enabled = lightSourceComponent.CurrentIntensity > 0;
        }
    }
}
