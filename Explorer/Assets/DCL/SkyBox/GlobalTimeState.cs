namespace DCL.SkyBox
{
    public class GlobalTimeState : ISkyboxState
    {
        private readonly SkyboxSettingsAsset settings;
        private float refreshAccumulatedTime;
        private float globalTimeOfDay;

        public GlobalTimeState(SkyboxSettingsAsset settings)
        {
            this.settings = settings;
        }

        public bool Applies() =>
            // Is the fallback state, so it should always apply
            true;

        public void Enter()
        {
            refreshAccumulatedTime = 0f;
            settings.CanUIControl = true;
        }

        public void Update(float dt)
        {
            globalTimeOfDay += dt * settings.SpeedMultiplier / SkyboxSettingsAsset.SECONDS_IN_DAY;

            while (globalTimeOfDay >= 1f)
                globalTimeOfDay -= 1f;

            refreshAccumulatedTime += dt;

            if (refreshAccumulatedTime >= settings.refreshInterval)
            {
                settings.TimeOfDayNormalized = globalTimeOfDay;
                settings.ShouldUpdateSkybox = true;
                refreshAccumulatedTime = 0f;
            }
        }

        public void Exit() { }
    }
}
