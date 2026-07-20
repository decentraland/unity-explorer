using DCL.Utility;

namespace DCL.Ipfs
{
    public static class AssetBundleManifestVersionExtensions
    {
        /// <summary>
        ///     Composes the platform-suffixed hash for a bare hash, resolved to the verbatim digest-bearing
        ///     manifest file name (<c>&lt;hash&gt;_&lt;depsDigest&gt;_&lt;platform&gt;</c>) when the manifest lists one —
        ///     the name that actually exists on the CDN and the sole identity used by every cache layer
        ///     downstream. Falls back to <c>&lt;hash&gt;_&lt;platform&gt;</c> otherwise (null or pre-v49 manifests,
        ///     files without a digest).
        /// </summary>
        public static string GetCdnRequestHash(this AssetBundleManifestVersion? manifest, string bareHash) =>
            manifest != null && manifest.TryGetFileNameWithDigest(bareHash, out string fileName)
                ? fileName
                : $"{bareHash}{PlatformUtils.GetCurrentPlatform()}";

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
