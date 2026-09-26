namespace DCL.ResourcesUnloading
{
    public interface ICacheCleaner
    {
        void UnloadCache(bool budgeted = true);

        /// <summary>
        ///     Evict a single raw-GLTF model (parsed import and instantiated container asset) by its
        ///     content-mapping hash, leaving every other cache warm. Used on a scene reload when the dev
        ///     server told us exactly which model changed, so we can avoid draining the whole cache.
        /// </summary>
        void EvictGltfModel(string hash);

        void UpdateProfilingCounters();
    }
}
