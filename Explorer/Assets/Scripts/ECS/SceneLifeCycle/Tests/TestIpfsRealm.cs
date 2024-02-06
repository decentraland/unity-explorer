using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Profiles;
using Ipfs;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ECS.SceneLifeCycle.Tests
{
    public class TestIpfsRealm : IIpfsRealm
    {
        public URLDomain LambdasBaseUrl { get; }
        public IReadOnlyList<string> SceneUrns { get; }
        public URLDomain EntitiesActiveEndpoint { get; }

        public URLDomain CatalystBaseUrl { get; }
        public URLDomain ContentBaseUrl { get; }

        public TestIpfsRealm(string[] sceneUrns = null)
        {
            SceneUrns = sceneUrns ?? Array.Empty<string>();
            CatalystBaseUrl = URLDomain.FromString($"file://{Application.dataPath + "/../TestResources/"}");
            ContentBaseUrl = CatalystBaseUrl.Append(URLSubdirectory.FromString("Content/"));
            EntitiesActiveEndpoint = URLDomain.FromString($"{ContentBaseUrl.Value}ActiveEntitiesByPointer.json");
            LambdasBaseUrl = URLDomain.EMPTY;
        }

        public UniTask PublishAsync<T>(IpfsRealmEntity<T> entity, IReadOnlyDictionary<string, byte[]> contentFiles, CancellationToken ct) =>
            throw new NotSupportedException();

        public string GetFileHash(byte[] file) =>
            file.IpfsHashV1();
    }
}
