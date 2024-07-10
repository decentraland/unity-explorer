using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Ipfs
{
    public class InvalidIpfsRealm : IIpfsRealm
    {
        private static InvalidIpfsRealm? sharedInstance;

        public static InvalidIpfsRealm Instance
        {
            get
            {
                return sharedInstance ??= new InvalidIpfsRealm();
            }
        }

        public URLDomain CatalystBaseUrl => URLDomain.EMPTY;
        public URLDomain ContentBaseUrl => URLDomain.EMPTY;
        public URLDomain LambdasBaseUrl => URLDomain.EMPTY;
        public IReadOnlyList<string> SceneUrns { get; } = new List<string>();
        public URLDomain EntitiesActiveEndpoint => URLDomain.EMPTY;

        public UniTask PublishAsync<T>(EntityDefinitionGeneric<T> entity, CancellationToken ct, IReadOnlyDictionary<string, byte[]>? contentFiles = null) =>
            throw new NotSupportedException();

        public string GetFileHash(byte[] file) =>
            throw new NotSupportedException();
    }
}
