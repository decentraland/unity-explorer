namespace ECS.Profiling
{
    public interface IProfilerView
    {
        bool IsOpen { get; }

        void SetFPS(float averageFrameTimeInSeconds);

        void SetHiccups(int hiccupCount);
    }
}
