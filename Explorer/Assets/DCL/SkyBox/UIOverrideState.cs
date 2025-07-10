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

        public bool Applies() =>
            skyboxSettings is { IsUIControlled: true, CanUIControl: true };

        public void Enter()
        {
            transition.Enter();
        }

        public void Update(float dt)
        {
            transition.Update(dt);
        }

        public void Exit()
        {
            transition.Exit();
        }
    }
}
