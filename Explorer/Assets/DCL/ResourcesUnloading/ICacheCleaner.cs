namespace DCL.ResourcesUnloading
{
    public interface ICacheCleaner
    {
        void UnloadCache(bool budgeted = true);
        void UpdateProfilingCounters();
    }
}
