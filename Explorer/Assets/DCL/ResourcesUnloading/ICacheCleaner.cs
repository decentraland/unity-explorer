namespace DCL.ResourcesUnloading
{
    public interface ICacheCleaner
    {
        void UnloadCache();

        void UpdateProfilingCounters();

        /// <summary>
        /// Frees the memory related to cached road assets (see RoadAssetPool).
        /// </summary>
        void UnloadRoadCacheOnly();
    }
}
