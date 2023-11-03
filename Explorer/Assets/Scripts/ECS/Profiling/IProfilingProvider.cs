namespace ECS.Profiling
{
    public interface IProfilingProvider
    {
        long TotalUsedMemoryInBytes { get; }

        float TotalUsedMemoryInMB { get; }

        long GetCurrentFrameTimeValueInNS();

        double GetAverageFrameTimeValueInNS();

        ulong GetHiccupCountInBuffer();

        void CheckHiccup();
    }
}
