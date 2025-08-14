namespace DCL.SkyBox
{
    public class UIOverrideState : ISkyboxState
    {
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly InterpolateTimeOfDayState transition;

        public UIOverrideState(SkyboxSettingsAsset skyboxSettings,
            InterpolateTimeOfDayState transition)
        {
            this.skyboxSettings = skyboxSettings;
            this.transition = transition;
        }

        // The logic of this behavior is processed at SkyboxMenuController
        public bool Applies() =>
            skyboxSettings is { IsUIControlled: true };

        public void Enter()
        {
            skyboxSettings.TransitionMode = TransitionMode.FORWARD;
            skyboxSettings.TargetTimeOfDayNormalized = skyboxSettings.UIOverrideTimeOfDayNormalized;
        }

        public void Update(float dt)
        {
            if (!transition.Applies()) return;
            transition.Update(dt);
        }

        public void Exit()
        {
        }
    }
}
