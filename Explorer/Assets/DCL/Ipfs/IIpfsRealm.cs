using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using System.Collections.Generic;
using System.Threading;

namespace Ipfs
{
    public interface IIpfsRealm
    {
        public URLDomain CatalystBaseUrl { get; }
        public URLDomain ContentBaseUrl { get; }
        public URLDomain LambdasBaseUrl { get; }
        public IReadOnlyList<string> SceneUrns { get; }
        public URLDomain EntitiesActiveEndpoint { get; }

        UniTask PublishAsync<T>(IpfsRealmEntity<T> entity, IReadOnlyDictionary<string, byte[]> contentFiles, CancellationToken ct);

        string GetFileHash(byte[] file);
    }
}
