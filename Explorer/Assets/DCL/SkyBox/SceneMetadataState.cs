using DCL.Ipfs;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using ECS.SceneLifeCycle;
using UnityEngine;
using static DCL.Ipfs.SceneMetadata;

namespace DCL.SkyBox
{
    public class SceneMetadataState : ISkyboxState
    {
        private readonly IScenesCache scenes;
        private readonly SkyboxSettingsAsset settings;
        private readonly ISceneRestrictionBusController sceneRestrictionController;
        private readonly InterpolateTimeOfDayState transition;

        public SceneMetadataState(IScenesCache scenes,
            SkyboxSettingsAsset settings,
            ISceneRestrictionBusController sceneRestrictionController,
            InterpolateTimeOfDayState transition)
        {
            this.scenes = scenes;
            this.settings = settings;
            this.sceneRestrictionController = sceneRestrictionController;
            this.transition = transition;
        }

        public bool Applies()
        {
            var metadata = GetCurrentMetadata();
            return metadata != null && GetFixedTime(metadata).HasValue;
        }

        public void Enter()
        {
            sceneRestrictionController.PushSceneRestriction(
                SceneRestriction.CreateSkyboxTimeUILocked(SceneRestrictionsAction.APPLIED));

            settings.IsDayCycleEnabled = false;
            TryApplySceneMetadata();
            transition.Enter();
        }

        public void Exit()
        {
            sceneRestrictionController.PushSceneRestriction(
                SceneRestriction.CreateSkyboxTimeUILocked(SceneRestrictionsAction.REMOVED));

            transition.Exit();
        }

        public void Update(float dt)
        {
            TryApplySceneMetadata();
            transition.Update(dt);
        }

        private SceneMetadata? GetCurrentMetadata() =>
            scenes.CurrentScene.Value?.SceneData.SceneEntityDefinition.metadata;

        private void TryApplySceneMetadata()
        {
            var metadata = GetCurrentMetadata();
            if (metadata == null)
                return;

            settings.TransitionMode = GetTransitionMode(metadata);

            float? fixedTime = GetFixedTime(metadata);
            if (!fixedTime.HasValue)
                return;

            float normalizedTime = SkyboxSettingsAsset.NormalizeTime(fixedTime.Value);

            // Skip if target hasn't changed to avoid transitioning
            if (Mathf.Approximately(settings.TargetTimeOfDayNormalized, normalizedTime))
                return;

            settings.TargetTimeOfDayNormalized = normalizedTime;
        }

        /// <summary>
        /// Retrieves the fixed time value for the skybox.
        /// Prioritizes scene-level skyboxConfig over worldConfiguration.SkyboxConfig for backward compatibility.
        /// </summary>
        /// <param name="metadata">The scene metadata containing skybox configuration</param>
        /// <returns>
        /// The fixed time value if skyboxConfig exists (even if null),
        /// otherwise falls back to worldConfiguration.SkyboxConfig.fixedTime for legacy scenes
        /// </returns>
        private static float? GetFixedTime(SceneMetadata metadata) =>
            metadata.skyboxConfig.HasValue
                ? metadata.skyboxConfig.Value.fixedTime
                : metadata.worldConfiguration?.SkyboxConfig?.fixedTime;

        /// <summary>
        /// Retrieves the transition mode for the skybox.
        /// Prioritizes scene-level skyboxConfig over worldConfiguration.SkyboxConfig for backward compatibility.
        /// </summary>
        /// <param name="sceneMetadata">The scene metadata containing skybox configuration</param>
        /// <returns>
        /// The transition mode from skyboxConfig if it exists,
        /// otherwise falls back to worldConfiguration.SkyboxConfig.transitionMode for legacy scenes,
        /// or TransitionMode.FORWARD as the final default
        /// </returns>
        private TransitionMode GetTransitionMode(SceneMetadata sceneMetadata) =>
            sceneMetadata.skyboxConfig.HasValue
                ? sceneMetadata.skyboxConfig.Value.transitionMode
                : sceneMetadata.worldConfiguration?.SkyboxConfig?.transitionMode ?? TransitionMode.FORWARD;
    }
}
