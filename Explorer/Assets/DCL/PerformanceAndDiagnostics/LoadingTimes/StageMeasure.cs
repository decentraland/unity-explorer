using DCL.RealmNavigation;

namespace DCL.LoadingTimes
{
    public struct StageMeasure
    {
        public readonly LoadingStatus.LoadingStage Stage;
        public readonly float StartTime;
        public float StopTime;

        public StageMeasure(LoadingStatus.LoadingStage stage, float startTime)
        {
            Stage = stage;
            StartTime = startTime;
            StopTime = 0f;
        }

        public override string ToString() =>
            $"{Stage} - ({StartTime}, {StopTime}) -> {StopTime - StartTime}";
    }
}
