namespace ECS.Profiling
{
    public interface IProfilerView
    {
        void SetFPS(float averageFrameTimeInSeconds);
        void SetHiccups(int hiccupCount);

        bool IsOpen { get; }
    }
}
