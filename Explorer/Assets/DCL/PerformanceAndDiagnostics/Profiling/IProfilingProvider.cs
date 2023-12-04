namespace DCL.Profiling
{
    public interface IProfilingProvider
    {
        ulong TotalUsedMemoryInBytes { get; }

        ulong CurrentFrameTimeValueInNS { get; }

        double AverageFrameTimeValueInNS { get; }

        ulong HiccupCountInBuffer { get; }

        void CheckHiccup();
    }
}
