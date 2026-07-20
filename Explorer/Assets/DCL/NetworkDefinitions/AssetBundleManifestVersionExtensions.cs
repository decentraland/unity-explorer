namespace DCL.Ipfs
{
    public static class AssetBundleManifestVersionExtensions
    {
        /// <summary>
        ///     Composes the cache key for an asset bundle: the verbatim digest-bearing manifest file name when the
        ///     manifest has an entry for the bare hash, or just the hash when it doesn't. Used by upper-layer caches
        ///     (GLTF container, etc.) to differentiate two scenes that share an AB hash but resolve different
        ///     dependency closures.
        /// </summary>
        public static string ComposeCacheKey(this AssetBundleManifestVersion? manifest, string hash) =>
            manifest != null && manifest.TryGetFileNameWithDigest(hash, out string fileName) ? fileName : hash;
    }
}
