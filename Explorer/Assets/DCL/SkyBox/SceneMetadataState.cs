using DCL.Ipfs;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using ECS.SceneLifeCycle;
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
            SceneMetadata? metadata = scenes.CurrentScene?.SceneData.SceneEntityDefinition.metadata;

            if (metadata == null) return false;

            return GetConfigFixedTime(metadata) != null;
        }

        public void Enter()
        {
            transition.Enter();

            SceneMetadata? sceneMetadata = scenes.CurrentScene?.SceneData.SceneEntityDefinition.metadata;

            if (sceneMetadata != null)
                UpdateSkyboxSettings(sceneMetadata);

            sceneRestrictionController.PushSceneRestriction(SceneRestriction.CreateSkyboxTimeUILocked(SceneRestrictionsAction.APPLIED));
        }

        public void Exit()
        {
            transition.Exit();
            sceneRestrictionController.PushSceneRestriction(SceneRestriction.CreateSkyboxTimeUILocked(SceneRestrictionsAction.REMOVED));
        }

        public void Update(float dt)
        {
            transition.Update(dt);
        }

        private void UpdateSkyboxSettings(SceneMetadata metadata)
        {
            // Scene config overrides world config
            float? time = GetConfigFixedTime(metadata);
            TransitionMode transitionMode = GetConfigTransitionModeOrDefault(metadata);

            settings.TransitionMode = transitionMode;

            if (time.HasValue)
                settings.TargetTimeOfDayNormalized = SkyboxSettingsAsset.NormalizeTime(time.Value);
        }

        private float? GetConfigFixedTime(SceneMetadata sceneMetadata) =>
            sceneMetadata.skyboxConfig?.fixedTime ?? sceneMetadata.worldConfiguration?.SkyboxConfig?.fixedTime;

        private TransitionMode GetConfigTransitionModeOrDefault(SceneMetadata sceneMetadata) =>
            sceneMetadata.skyboxConfig?.transitionMode
            ?? sceneMetadata.worldConfiguration?.SkyboxConfig?.transitionMode
            ?? TransitionMode.FORWARD;
    }
}
