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

            return metadata.skyboxConfig != null || metadata.worldConfiguration?.SkyboxConfig != null;
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
            // Extract world and scene configs (if any)
            SkyboxConfigData? worldConfig = metadata.worldConfiguration?.SkyboxConfig;
            SkyboxConfigData? sceneConfig = metadata.skyboxConfig;

            // Scene config overrides world config
            float? time = sceneConfig?.fixedTime ?? worldConfig?.fixedTime;
            TransitionMode transitionMode = sceneConfig?.transitionMode ?? worldConfig?.transitionMode ?? TransitionMode.FORWARD;

            settings.TransitionMode = transitionMode;

            if (time.HasValue)
                settings.TargetTimeOfDayNormalized = SkyboxSettingsAsset.NormalizeTime(time.Value);
        }
    }
}
