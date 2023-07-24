namespace ECS.Profiling
{

    public interface IProfilingProvider
    {
        public long GetCurrentFrameTimeValueInNS();

        public float GetFrameRate();

        public int GetHiccupValue(HiccupKey miliseconds);
    }

    public enum HiccupKey { FiftyMS, FourtyMS, ThirtyMS };

}
