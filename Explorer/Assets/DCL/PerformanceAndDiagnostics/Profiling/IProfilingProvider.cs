namespace DCL.Profiling
{
    public interface IProfilingProvider
    {
        ulong TotalUsedMemoryInBytes { get; }

        ulong CurrentFrameTimeValueInNS { get; }

        double AverageFrameTimeValueInNS { get; }

        ulong HiccupCountInBuffer { get; }

        long MinFrameTimeValueInNS { get; }

        long MaxFrameTimeValueInNS { get; }

        void CheckHiccup();
    }
}
