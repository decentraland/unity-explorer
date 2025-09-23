using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using ECS.SceneLifeCycle;

namespace DCL.SkyBox
{
    public class SDKComponentState : ISkyboxState
    {
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;
        private readonly InterpolateTimeOfDayState transition;
        private readonly IScenesCache scenes;

        public SDKComponentState(SkyboxSettingsAsset skyboxSettings,
            ISceneRestrictionBusController sceneRestrictionBusController,
            InterpolateTimeOfDayState transition,
            IScenesCache scenes)
        {
            this.skyboxSettings = skyboxSettings;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
            this.transition = transition;
            this.scenes = scenes;
        }

        // The logic of this behavior is mostly processed at SkyboxTimeHandlerSystem
        public bool Applies() =>
            skyboxSettings.CurrentSDKControlledScene != null
            && scenes.CurrentScene?.Info.BaseParcel == skyboxSettings.CurrentSDKControlledScene;

        public void Enter()
        {
            transition.Enter();
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateSkyboxTimeUILocked(SceneRestrictionsAction.APPLIED));
        }

        public void Update(float dt)
        {
            transition.Update(dt);
        }

        public void Exit()
        {
            transition.Exit();
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateSkyboxTimeUILocked(SceneRestrictionsAction.REMOVED));
        }
    }
}
