namespace DCL.Profiling
{
    public interface IProfilingProvider
    {
        ulong TotalUsedMemoryInBytes { get; }

        ulong CurrentFrameTimeValueInNS { get; }

        long CurrentGPUFrameTimeValueInNS { get; }

        double AverageFrameTimeValueInNS { get; }
        int AverageFameTimeSamples { get; }

        ulong HiccupCountInBuffer { get; }
        int HiccupCountBufferSize { get; }

        long MinFrameTimeValueInNS { get; }

        long MaxFrameTimeValueInNS { get; }

        float GcAllocatedInFrameRecorder { get; }

        void CheckHiccup();
    }
}
