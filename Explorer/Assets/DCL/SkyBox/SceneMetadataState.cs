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
            scenes.CurrentScene?.SceneData.SceneEntityDefinition.metadata;

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

        private static float? GetFixedTime(SceneMetadata metadata) =>
            metadata.skyboxConfig?.fixedTime
            ?? metadata.worldConfiguration?.SkyboxConfig?.fixedTime;

        private TransitionMode GetTransitionMode(SceneMetadata sceneMetadata) =>
            sceneMetadata.skyboxConfig?.transitionMode
            ?? sceneMetadata.worldConfiguration?.SkyboxConfig?.transitionMode
            ?? TransitionMode.FORWARD;
    }
}
