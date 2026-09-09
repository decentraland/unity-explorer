using CommunicationData.URLHelpers;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    /// <summary>
    ///     Evicts corrupt entries from Unity's built-in <see cref="Caching" />: a corrupt archive completes its
    ///     web request (cache hit) yet mounts to a null bundle, and Unity never evicts it on its own.
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
        ///     Main thread only. Returns false when there was no entry for that url+hash pair or the entry is in use.
        /// </summary>
        internal static bool TryEvict(URLAddress url, Hash128 cacheHash) =>
            Caching.ClearCachedVersion(CacheNameFromUrl(url), cacheHash);
    }
}
