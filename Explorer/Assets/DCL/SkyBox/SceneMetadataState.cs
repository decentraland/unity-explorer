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

        public SceneMetadataState(IScenesCache scenes,
            SkyboxSettingsAsset settings,
            ISceneRestrictionBusController sceneRestrictionController)
        {
            this.scenes = scenes;
            this.settings = settings;
            this.sceneRestrictionController = sceneRestrictionController;
        }

        public bool Applies()
        {
            SceneMetadata? metadata = scenes.CurrentScene?.SceneData.SceneEntityDefinition.metadata;

            if (metadata == null) return false;

            return metadata.worldConfiguration != null || metadata.skyboxConfig != null;
        }

        public void Enter()
        {
            // TODO: should be called in the update? Are we safe on just calling it here?
            SceneMetadata? sceneMetadata = scenes.CurrentScene?.SceneData.SceneEntityDefinition.metadata;

            if (sceneMetadata is { worldConfiguration: { SkyboxConfig: { fixedTime: var worldTime } } })
                ApplyFixedTime(worldTime);

            if (sceneMetadata is { skyboxConfig: { fixedTime: var sceneTime } })
                ApplyFixedTime(sceneTime);
        }

        public void Update(float dt) { }

        public void Exit() { }

        private void ApplyFixedTime(float time)
        {
            settings.IsDayCycleEnabled = false;
            settings.TransitionMode = TransitionMode.FORWARD;
            settings.TargetTransitionTimeOfDay = time;
            settings.CanUIControl = false;

            sceneRestrictionController.PushSceneRestriction(SceneRestriction.CreateSkyboxTimeUILocked(SceneRestrictionsAction.APPLIED));
        }
    }
}
