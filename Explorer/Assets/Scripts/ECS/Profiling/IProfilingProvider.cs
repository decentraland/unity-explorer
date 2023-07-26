namespace ECS.Profiling
{

    public interface IProfilingProvider
    {
        public long GetCurrentFrameTimeValueInNS();

        public double GetAverageFrameTimeValueInNS();

        public int GetHiccupCountInBuffer();
    }


}
