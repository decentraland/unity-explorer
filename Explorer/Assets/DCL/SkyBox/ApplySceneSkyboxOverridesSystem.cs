using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Quality;
using DCL.SkyBox.Components;
using ECS.Abstract;
using UnityEngine;

namespace DCL.SkyBox
{
    /// <summary>
    ///     Pushes the environment overrides requested by the current scene to the visible sky and to the reflection cubemap.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [LogCategory(ReportCategory.SKYBOX)]
    public partial class ApplySceneSkyboxOverridesSystem : BaseUnityLoopSystem
    {
        private readonly IRendererFeaturesCache rendererFeaturesCache;
        private readonly SkyboxRenderController skyboxRenderController;
        private readonly Entity skyboxEntity;

        internal ApplySceneSkyboxOverridesSystem(World world,
            IRendererFeaturesCache rendererFeaturesCache,
            SkyboxRenderController skyboxRenderController,
            Entity skyboxEntity) : base(world)
        {
            this.rendererFeaturesCache = rendererFeaturesCache;
            this.skyboxRenderController = skyboxRenderController;
            this.skyboxEntity = skyboxEntity;
        }

        protected override void Update(float t)
        {
            ref SceneSkyboxOverrides overrides = ref World.Get<SceneSkyboxOverrides>(skyboxEntity);

            // Sky first: on clear the renderer feature then sees the restored skybox material and the cleared override in the same frame
            if (!ReferenceEquals(overrides.SkyboxTexture, overrides.AppliedSkyboxTexture))
            {
                skyboxRenderController.SetSkyboxOverride(overrides.SkyboxTexture);
                overrides.AppliedSkyboxTexture = overrides.SkyboxTexture;
            }

            Texture2D? reflectionSource = overrides.ReflectionSource;

            if (ReferenceEquals(reflectionSource, overrides.AppliedReflectionSource))
                return;

            // The feature exists on the High quality tier only; the push stays pending until it is available
            SkyboxToCubemapRendererFeature? feature = rendererFeaturesCache.GetRendererFeature<SkyboxToCubemapRendererFeature>();

            if (feature == null)
                return;

            feature.SetReflectionOverride(reflectionSource);
            overrides.AppliedReflectionSource = reflectionSource;
        }
    }
}
