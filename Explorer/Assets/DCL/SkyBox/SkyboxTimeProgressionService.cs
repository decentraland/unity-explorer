namespace DCL.SkyBox
{
    public class SkyboxTimeProgressionService
    {
        private readonly SkyboxSettingsAsset settings;
        private readonly InterpolateTimeOfDayState transition;
        private float refreshAccumulatedTime;
        private float globalTimeOfDay;
        private bool isTransitioning;

        public SkyboxTimeProgressionService(SkyboxSettingsAsset settings,
            InterpolateTimeOfDayState transition)
        {
            this.settings = settings;
            this.transition = transition;

            globalTimeOfDay = settings.TimeOfDayNormalized;
        }

        public void Reset()
        {
            refreshAccumulatedTime = 0f;
            settings.TargetTimeOfDayNormalized = globalTimeOfDay;
            isTransitioning = true;
        }

        public void UpdateTimeProgression(float deltaTime)
        {
            if (isTransitioning)
            {
                if (transition.Applies())
                {
                    transition.Update(deltaTime);
                    return;
                }

                isTransitioning = false;
            }

            globalTimeOfDay += deltaTime * settings.FullCycleSpeed;

            while (globalTimeOfDay >= 1f)
                globalTimeOfDay -= 1f;

            refreshAccumulatedTime += deltaTime;

            if (refreshAccumulatedTime >= settings.RefreshInterval)
            {
                settings.TimeOfDayNormalized = globalTimeOfDay;
                refreshAccumulatedTime = 0f;
            }
        }
    }
}
