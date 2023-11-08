namespace DCL.Profiling
{
    public interface IProfilingProvider
    {
        long TotalUsedMemoryInBytes { get; }

        float TotalUsedMemoryInMB { get; }

        long CurrentFrameTimeValueInNS { get; }

        double AverageFrameTimeValueInNS { get; }

        ulong HiccupCountInBuffer { get; }

        void CheckHiccup();
    }
}
