namespace ECS.Profiling
{
    public interface IProfilingProvider
    {
        long GetCurrentFrameTimeValueInNS();

        double GetAverageFrameTimeValueInNS();

        int GetHiccupCountInBuffer();

        void CheckHiccup();
    }
}
