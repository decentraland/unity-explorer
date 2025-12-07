using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
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
        public URLDomain EntitiesActiveEndpoint { get; }
        public URLDomain AssetBundleRegistryEntitiesActive { get; }

        public UniTask PublishAsync<T>(EntityDefinitionGeneric<T> entity, CancellationToken ct, JsonSerializerSettings? serializerSettings = null, IReadOnlyDictionary<string, byte[]>? contentFiles = null);

        string GetFileHash(byte[] file);
    }
}
