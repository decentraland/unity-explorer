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
        private readonly SkyboxTimeProgressionService timeProgressionService;

        public SceneMetadataState(IScenesCache scenes,
            SkyboxSettingsAsset settings,
            ISceneRestrictionBusController sceneRestrictionController,
            InterpolateTimeOfDayState transition,
            SkyboxTimeProgressionService timeProgressionService)
        {
            this.scenes = scenes;
            this.settings = settings;
            this.sceneRestrictionController = sceneRestrictionController;
            this.transition = transition;
            this.timeProgressionService = timeProgressionService;
        }

        public bool Applies()
        {
            SceneMetadata? metadata = GetCurrentSceneMetadata();

            if (metadata == null) return false;

            return metadata.skyboxConfig != null || metadata.worldConfiguration?.SkyboxConfig != null;
        }

        public void Enter()
        {
            sceneRestrictionController.PushSceneRestriction(SceneRestriction.CreateSkyboxTimeUILocked(SceneRestrictionsAction.APPLIED));

            SceneMetadata? sceneMetadata = GetCurrentSceneMetadata();
            if (sceneMetadata == null) return;
            settings.IsDayCycleEnabled = sceneMetadata.FixedTime == null;

            if (settings.IsDayCycleEnabled)
                timeProgressionService.Reset();
            else
                UpdateSkyboxSettings(sceneMetadata);

            transition.Enter();
        }

        public void Exit()
        {
            sceneRestrictionController.PushSceneRestriction(SceneRestriction.CreateSkyboxTimeUILocked(SceneRestrictionsAction.REMOVED));
            transition.Exit();
        }

        public void Update(float dt)
        {
            SceneMetadata? sceneMetadata = GetCurrentSceneMetadata();
            if (sceneMetadata == null) return;
            settings.IsDayCycleEnabled = sceneMetadata.FixedTime == null;

            if (settings.IsDayCycleEnabled)
                timeProgressionService.UpdateTimeProgression(dt);
            else
                transition.Update(dt);
        }

        private void UpdateSkyboxSettings(SceneMetadata metadata)
        {
            settings.TransitionMode = metadata.GetTransitionModeOrDefault();

            float? time = metadata.FixedTime;

            if (time.HasValue)
                settings.TargetTimeOfDayNormalized = SkyboxSettingsAsset.NormalizeTime(time.Value);
        }

        private SceneMetadata? GetCurrentSceneMetadata() =>
            scenes.CurrentScene?.SceneData?.SceneEntityDefinition?.metadata;
    }
}
