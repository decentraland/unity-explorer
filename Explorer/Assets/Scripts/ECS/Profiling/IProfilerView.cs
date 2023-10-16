namespace ECS.Profiling
{
    public interface IProfilerView
    {
        bool IsOpen { get; }

        void SetMemory(float totalUsedMemoryInMB);

        void SetFPS(float averageFrameTimeInSeconds);

        void SetHiccups(int hiccupCount);
    }
}
