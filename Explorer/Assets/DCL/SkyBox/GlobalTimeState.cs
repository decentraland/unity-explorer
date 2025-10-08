namespace DCL.SkyBox
{
    public class GlobalTimeState : ISkyboxState
    {
        private readonly SkyboxSettingsAsset settings;
        private readonly InterpolateTimeOfDayState transition;
        private readonly SkyboxTimeProgressionService timeProgressionService;

        public GlobalTimeState(SkyboxSettingsAsset settings,
            InterpolateTimeOfDayState transition,
            SkyboxTimeProgressionService timeProgressionService)
        {
            this.settings = settings;
            this.transition = transition;
            this.timeProgressionService = timeProgressionService;
        }

        public bool Applies() =>
            // Is the fallback state, so it should always apply
            true;

        public void Enter()
        {
            settings.IsDayCycleEnabled = true;
            timeProgressionService.Reset();
            transition.Enter();
        }

        public void Update(float dt) =>
            timeProgressionService.UpdateTimeProgression(dt);

        public void Exit()
        {
            transition.Exit();
        }
    }
}
