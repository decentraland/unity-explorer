namespace ECS.Profiling
{
    public interface IProfilingProvider
    {
        long GetCurrentFrameTimeValueInNS();

        double GetAverageFrameTimeValueInNS();

        ulong GetHiccupCountInBuffer();

        void CheckHiccup();
    }
}
