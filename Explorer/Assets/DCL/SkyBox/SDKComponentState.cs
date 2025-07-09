namespace DCL.SkyBox
{
    public class SDKComponentState : ISkyboxState
    {
        private readonly SkyboxSettingsAsset skyboxSettings;

        public SDKComponentState(SkyboxSettingsAsset skyboxSettings)
        {
            this.skyboxSettings = skyboxSettings;
        }

        public bool Applies() =>
            skyboxSettings.IsSDKControlled;

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
