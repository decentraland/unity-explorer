using CommunicationData.URLHelpers;
using System;
using System.Collections.Generic;

namespace Ipfs
{
    /// <summary>
    ///     Provides a simple implementation to resolve URLs from StreamingAssets
    /// </summary>
    public class LocalIpfsRealm : IIpfsRealm
    {
        public LocalIpfsRealm(URLDomain fullPath)
        {
            CatalystBaseUrl = fullPath;
            ContentBaseUrl = fullPath;
        }

        public URLDomain CatalystBaseUrl { get; }
        public URLDomain ContentBaseUrl { get; }
        public IReadOnlyList<string> SceneUrns => Array.Empty<string>();
        public URLDomain EntitiesActiveEndpoint => URLDomain.EMPTY;
    }
}
