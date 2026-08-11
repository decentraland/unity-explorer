using CommunicationData.URLHelpers;
using System;
using System.Collections.Generic;

namespace DCL.Ipfs
{
    /// <summary>
    ///     Provides a simple implementation to resolve URLs from StreamingAssets
    /// </summary>
    public class LocalIpfsRealm : IIpfsRealm
    {
        public URLDomain EntitiesBaseUrl => throw new NotSupportedException();
        public URLDomain CatalystBaseUrl { get; }
        public URLDomain ContentBaseUrl { get; }
        public URLDomain LambdasBaseUrl { get; }
        public IReadOnlyList<string> SceneUrns => Array.Empty<string>();
        public URLDomain EntitiesActiveEndpoint => URLDomain.EMPTY;
        public URLDomain AssetBundleRegistryEntitiesActive { get; }

        public LocalIpfsRealm(URLDomain fullPath)
        {
            CatalystBaseUrl = fullPath;
            ContentBaseUrl = fullPath;
            AssetBundleRegistryEntitiesActive = fullPath;
            LambdasBaseUrl = URLDomain.FromString("https://peer.decentraland.org/explorer/");
        }

        public string GetFileHash(byte[] file) =>
            file.IpfsHashV1();
    }
}
