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
            lightSourceComponent.CurrentIntensityNormalized = Mathf.MoveTowards(lightSourceComponent.CurrentIntensityNormalized, isLightOn ? 1 : 0, dt);

            Light lightSourceInstance = lightSourceComponent.LightSourceInstance;

            lightSourceInstance.intensity = lightSourceComponent.MaxIntensity * lightSourceComponent.IntensityScale * lightSourceComponent.CurrentIntensityNormalized;

            bool computeRange = !pbLightSource.HasRange || pbLightSource.Range < 0;
            lightSourceInstance.range = computeRange ? Mathf.Pow(lightSourceComponent.MaxIntensity, settings.RangeFormulaExponent) : pbLightSource.Range;

            lightSourceInstance.enabled = lightSourceComponent.CurrentIntensityNormalized > 0;
        }
    }
}
