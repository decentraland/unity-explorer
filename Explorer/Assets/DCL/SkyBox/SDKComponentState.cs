using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;

namespace DCL.SkyBox
{
    public class SDKComponentState : ISkyboxState
    {
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;
        private readonly InterpolateTimeOfDayState transition;

        public SDKComponentState(SkyboxSettingsAsset skyboxSettings,
            ISceneRestrictionBusController sceneRestrictionBusController,
            InterpolateTimeOfDayState transition)
        {
            this.skyboxSettings = skyboxSettings;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
            this.transition = transition;
        }

        // The logic of this behavior is mostly processed at SkyboxTimeHandlerSystem
        public bool Applies() =>
            skyboxSettings.IsSDKControlled;

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
