using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Ipfs
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
        public URLDomain AssetBundleRegistry { get; }

        public LocalIpfsRealm(URLDomain fullPath)
        {
            CatalystBaseUrl = fullPath;
            ContentBaseUrl = fullPath;
            AssetBundleRegistry = fullPath;
            LambdasBaseUrl = URLDomain.FromString("https://peer.decentraland.org/explorer/");
        }

        public UniTask PublishAsync<T>(EntityDefinitionGeneric<T> entity, CancellationToken ct, JsonSerializerSettings? serializerSettings = null, IReadOnlyDictionary<string, byte[]>? contentFiles = null) =>
            throw new NotSupportedException();

        public string GetFileHash(byte[] file) =>
            file.IpfsHashV1();
    }
}
