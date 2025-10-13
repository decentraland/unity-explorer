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

            globalTimeOfDay += dt * settings.FullCycleSpeed;

            while (globalTimeOfDay >= 1f)
                globalTimeOfDay -= 1f;

            refreshAccumulatedTime += dt;

            if (refreshAccumulatedTime >= settings.RefreshInterval)
            {
                settings.TimeOfDayNormalized = globalTimeOfDay;
                refreshAccumulatedTime = 0f;
            }
        }

        public void Exit()
        {
            transition.Exit();
        }
    }
}
