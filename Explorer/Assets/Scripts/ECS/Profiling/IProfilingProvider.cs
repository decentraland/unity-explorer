namespace ECS.Profiling
{
    public interface IProfilingProvider
    {
        long CurrentFrameTimeValueInNS { get; }

        double AverageFrameTimeValueInNS { get; }

        int HiccupCountInBuffer { get; }

        float TotalUsedMemoryInMB { get; }

        void CheckHiccup();
    }
}
