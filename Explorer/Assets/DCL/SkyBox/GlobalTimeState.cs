namespace DCL.SkyBox
{
    public class GlobalTimeState : ISkyboxState
    {
        private readonly SkyboxSettingsAsset settings;
        private readonly InterpolateTimeOfDayState transition;
        private float refreshAccumulatedTime;
        private float globalTimeOfDay;
        private bool isTransitioning;

        public GlobalTimeState(SkyboxSettingsAsset settings,
            InterpolateTimeOfDayState transition)
        {
            this.settings = settings;
            this.transition = transition;
            globalTimeOfDay = settings.TimeOfDayNormalized;
        }

        public bool Applies() =>
            // Is the fallback state, so it should always apply
            true;

        public void Enter()
        {
            refreshAccumulatedTime = 0f;
            settings.CanUIControl = true;
            settings.TargetTimeOfDayNormalized = globalTimeOfDay;
            settings.IsDayCycleEnabled = true;
            isTransitioning = true;

            transition.Enter();
        }

        public void Update(float dt)
        {
            if (isTransitioning)
            {
                if (transition.Applies())
                {
                    transition.Update(dt);
                    return;
                }

                isTransitioning = false;
            }

            globalTimeOfDay += dt * (settings.SpeedMultiplier / SkyboxSettingsAsset.SECONDS_IN_DAY);

            while (globalTimeOfDay >= 1f)
                globalTimeOfDay -= 1f;

            refreshAccumulatedTime += dt;

            if (refreshAccumulatedTime >= SkyboxSettingsAsset.REFRESH_INTERVAL)
            {
                settings.TimeOfDayNormalized = globalTimeOfDay;
                settings.ShouldUpdateSkybox = true;
                refreshAccumulatedTime = 0f;
            }
        }

        public void Exit()
        {
            transition.Exit();
        }
    }
}
