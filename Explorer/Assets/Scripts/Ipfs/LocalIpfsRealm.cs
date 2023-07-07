using System;
using System.Collections.Generic;

namespace Ipfs
{
    /// <summary>
    ///     Provides a simple implementation to resolve URLs from StreamingAssets
    /// </summary>
    public class LocalIpfsRealm : IIpfsRealm
    {
        public LocalIpfsRealm(string fullPath)
        {
            CatalystBaseUrl = fullPath;
            ContentBaseUrl = fullPath;
        }

        public string CatalystBaseUrl { get; }
        public string ContentBaseUrl { get; }
        public IReadOnlyList<string> SceneUrns => Array.Empty<string>();
        public string EntitiesActiveEndpoint => string.Empty;
    }
}
