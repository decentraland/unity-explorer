using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Ipfs
{
    public interface IIpfsRealm
    {
        public URLDomain CatalystBaseUrl { get; }
        public URLDomain ContentBaseUrl { get; }
        public URLDomain LambdasBaseUrl { get; }
        public IReadOnlyList<string> SceneUrns { get; }
        public Uri EntitiesActiveEndpoint { get; }
        public Uri AssetBundleRegistry { get; }

        UniTask PublishAsync<T>(EntityDefinitionGeneric<T> entity, CancellationToken ct, IReadOnlyDictionary<string, byte[]>? contentFiles = null);

        string GetFileHash(byte[] file);
    }
}
