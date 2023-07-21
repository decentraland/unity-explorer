namespace ECS.Profiling
{

    public interface IProfilingProvider
    {
        public long GetCurrentFrameTimeValue();

        public float GetFrameRate();

        public int GetHiccupValue(HiccupKey miliseconds);
    }

    public enum HiccupKey { FiftyMS, FourtyMS, ThirtyMS };

}
