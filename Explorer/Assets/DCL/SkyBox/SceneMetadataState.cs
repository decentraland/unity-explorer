using DCL.Ipfs;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using ECS.SceneLifeCycle;

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

            if (sceneMetadata is { worldConfiguration: { SkyboxConfig: { fixedTime: var worldTime } } })
                ApplyFixedTime(worldTime);

            if (sceneMetadata is { skyboxConfig: { fixedTime: var sceneTime } })
                ApplyFixedTime(sceneTime);

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

        private void ApplyFixedTime(float time)
        {
            settings.TransitionMode = TransitionMode.FORWARD;
            settings.TargetTimeOfDayNormalized = SkyboxSettingsAsset.NormalizeTime(time);
        }
    }
}
