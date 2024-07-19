namespace DCL.Profiling
{
    public interface IProfilingProvider
    {
        ulong TotalUsedMemoryInBytes { get; }

        ulong CurrentFrameTimeValueInNS { get; }

        long LastFrameTimeValueInNS { get; }

        long LastGPUFrameTimeValueInNS { get; }

        double AverageFrameTimeValueInNS { get; }
        int AverageFameTimeSamples { get; }

        ulong HiccupCountInBuffer { get; }
        int HiccupCountBufferSize { get; }

        long MinFrameTimeValueInNS { get; }

        long MaxFrameTimeValueInNS { get; }

        void CheckHiccup();
    }
}
