using DCL.Utility;

namespace DCL.Ipfs
{
    public static class AssetBundleManifestVersionExtensions
    {
        /// <summary>Builds the hash requested from the CDN: the verbatim digest-bearing manifest entry when listed, otherwise the platform-suffixed bare hash.</summary>
        public static string GetCdnRequestHash(this AssetBundleManifestVersion? manifest, string bareHash) =>
            manifest != null && manifest.TryGetFileNameWithDigest(bareHash, out string fileName)
                ? fileName
                : $"{bareHash}{PlatformUtils.GetCurrentPlatform()}";

        /// <summary>Composes the upper-layer cache key (GLTF container, etc.): the verbatim digest-bearing file name when listed, otherwise the bare hash.</summary>
        public static string ComposeCacheKey(this AssetBundleManifestVersion? manifest, string hash) =>
            manifest != null && manifest.TryGetFileNameWithDigest(hash, out string fileName) ? fileName : hash;
    }
}
