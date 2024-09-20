namespace DCL.ResourcesUnloading
{
    public interface ICacheCleaner
    {
        void UnloadCache();

        void UpdateProfilingCounters();
    }
}
