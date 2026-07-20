using DCL.Utility;

namespace DCL.Ipfs
{
    public static class AssetBundleManifestVersionExtensions
    {
        /// <summary>Builds the hash requested from the CDN: the canonical manifest file name when known (digest-bearing, correctly cased), otherwise the platform-suffixed bare hash.</summary>
        public static string GetCdnRequestHash(this AssetBundleManifestVersion? manifest, string bareHash)
        {
            string platformHash = $"{bareHash}{PlatformUtils.GetCurrentPlatform()}";
            return manifest?.ResolveCdnRequestHash(platformHash) ?? platformHash;
        }

        /// <summary>Composes the upper-layer cache key (GLTF container, etc.): the canonical CDN file name when known, otherwise the bare hash.</summary>
        public static string ComposeCacheKey(this AssetBundleManifestVersion? manifest, string hash) =>
            manifest != null && manifest.TryResolveCdnRequestHash($"{hash}{PlatformUtils.GetCurrentPlatform()}", out string fileName) ? fileName : hash;
    }
}
