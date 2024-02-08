using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Ipfs
{
    /// <summary>
    ///     Provides a simple implementation to resolve URLs from StreamingAssets
    /// </summary>
    public class LocalIpfsRealm : IIpfsRealm
    {
        public URLDomain CatalystBaseUrl { get; }
        public URLDomain ContentBaseUrl { get; }
        public URLDomain LambdasBaseUrl { get; }
        public IReadOnlyList<string> SceneUrns => Array.Empty<string>();
        public URLDomain EntitiesActiveEndpoint => URLDomain.EMPTY;

        public LocalIpfsRealm(URLDomain fullPath)
        {
            CatalystBaseUrl = fullPath;
            ContentBaseUrl = fullPath;
            LambdasBaseUrl = URLDomain.FromString("https://peer.decentraland.org/explorer/");
        }

        public UniTask PublishAsync<T>(IpfsRealmEntity<T> entity, CancellationToken ct, IReadOnlyDictionary<string, byte[]>? contentFiles = null) =>
            throw new NotSupportedException();

        public string GetFileHash(byte[] file) =>
            file.IpfsHashV1();
    }
}
