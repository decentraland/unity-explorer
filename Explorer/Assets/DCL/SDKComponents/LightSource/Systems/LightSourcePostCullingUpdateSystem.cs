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
    [UpdateAfter(typeof(LightSourceCullingSystem))]
    [UpdateAfter(typeof(LightSourceLodSystem))]
    [LogCategory(ReportCategory.LIGHT_SOURCE)]
    public partial class LightSourcePostCullingUpdateSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly float fadeDuration;

        public LightSourcePostCullingUpdateSystem(World world, ISceneStateProvider sceneStateProvider, float fadeDuration) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.fadeDuration = fadeDuration;
        }

        protected override void Update(float t)
        {
            AnimateLightSourceIntensityQuery(World,  fadeDuration > 0 ? Time.unscaledDeltaTime / fadeDuration : 1);
        }

        [Query]
        private void AnimateLightSourceIntensity([Data] float dt, ref LightSourceComponent lightSourceComponent, in PBLightSource pbLightSource)
        {
            Light lightSourceInstance = lightSourceComponent.LightSourceInstance;

            if (!LightSourceHelper.IsPBLightSourceActive(pbLightSource)) return;

            bool isLightOn = sceneStateProvider.IsCurrent && !lightSourceComponent.IsCulled;
            lightSourceComponent.TargetIntensity = isLightOn ? lightSourceComponent.MaxIntensity : 0;

            float delta = dt * lightSourceComponent.MaxIntensity;
            lightSourceComponent.CurrentIntensity = Mathf.MoveTowards(lightSourceComponent.CurrentIntensity, lightSourceComponent.TargetIntensity, delta);

            lightSourceInstance.intensity = lightSourceComponent.CurrentIntensity;

            lightSourceInstance.enabled = lightSourceComponent.CurrentIntensity > 0;
        }
    }
}
