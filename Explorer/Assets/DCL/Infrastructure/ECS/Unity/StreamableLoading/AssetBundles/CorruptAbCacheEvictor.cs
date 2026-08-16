using CommunicationData.URLHelpers;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Maintains the invariant that an entry in Unity's built-in <see cref="Caching" /> is either mountable or absent:
    ///     a corrupt cached archive completes its web request successfully (cache hit, no network) yet yields a null
    ///     bundle from the native mount, and Unity never evicts such an entry on its own.
    /// </summary>
    internal static class CorruptAbCacheEvictor
    {
        /// <summary>
        ///     Unity's <see cref="Caching" /> keys entries by the file name of the request URL (query string excluded).
        /// </summary>
        internal static string CacheNameFromUrl(URLAddress url)
        {
            string value = url.Value;
            int end = value.IndexOf('?');

            if (end < 0)
                end = value.Length;

            int lastSlash = value.LastIndexOf('/', end - 1);
            return value.Substring(lastSlash + 1, end - lastSlash - 1);
        }

        /// <summary>
        ///     Main thread only. Returns false when there was no entry for that url+hash pair or the entry is in use
        ///     (a corrupt entry never mounts, so it can never be in use).
        /// </summary>
        internal static bool TryEvict(URLAddress url, Hash128 cacheHash) =>
            Caching.ClearCachedVersion(CacheNameFromUrl(url), cacheHash);
    }
}
