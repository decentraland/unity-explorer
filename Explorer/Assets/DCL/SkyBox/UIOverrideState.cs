namespace DCL.SkyBox
{
    public class UIOverrideState : ISkyboxState
    {
        private readonly SkyboxSettingsAsset skyboxSettings;

        public UIOverrideState(SkyboxSettingsAsset skyboxSettings)
        {
            this.skyboxSettings = skyboxSettings;
        }

        public bool Applies() =>
            skyboxSettings is { IsUIControlled: true, CanUIControl: true };

        public void Enter()
        {
        }

        public void Update(float dt)
        {
        }

        public void Exit()
        {
        }
    }
}
