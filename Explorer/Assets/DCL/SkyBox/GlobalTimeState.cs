namespace DCL.SkyBox
{
    public class GlobalTimeState : ISkyboxState
    {
        private readonly SkyboxSettingsAsset settings;
        private readonly InterpolateTimeOfDayState transition;
        private readonly bool paused;
        private float refreshAccumulatedTime;
        private bool isTransitioning;

        public GlobalTimeState(SkyboxSettingsAsset settings,
            InterpolateTimeOfDayState transition,
            bool paused = false)
        {
            this.settings = settings;
            this.transition = transition;
            this.paused = paused;
        }

        public bool Applies() =>
            // Is the fallback state, so it should always apply
            true;

        public void Enter()
        {
            refreshAccumulatedTime = 0f;
            settings.TargetTimeOfDayNormalized = settings.GlobalTimeOfDayNormalized;
            settings.IsDayCycleEnabled = true;
            isTransitioning = true;

            transition.Enter();
        }

        public void Update(float dt)
        {
            if (paused) return;

            if (isTransitioning)
            {
                if (transition.Applies())
                {
                    transition.Update(dt);
                    return;
                }

                isTransitioning = false;
            }

            refreshAccumulatedTime += dt;

            if (refreshAccumulatedTime < settings.RefreshInterval) return;

            settings.TimeOfDayNormalized = settings.GlobalTimeOfDayNormalized;
            refreshAccumulatedTime -= settings.RefreshInterval;
        }

        public void Exit()
        {
            transition.Exit();
        }
    }
}
