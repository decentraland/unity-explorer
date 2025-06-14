using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Ipfs;
using DCL.Profiles;
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
        public Uri EntitiesActiveEndpoint { get; }
        public Uri AssetBundleRegistry { get; }

        public URLDomain CatalystBaseUrl { get; }
        public URLDomain ContentBaseUrl { get; }

        public TestIpfsRealm(string[]? sceneUrns = null)
        {
            SceneUrns = sceneUrns ?? Array.Empty<string>();
            CatalystBaseUrl = URLDomain.FromString($"file://{Application.dataPath + "/../TestResources/"}");
            ContentBaseUrl = CatalystBaseUrl.Append(URLSubdirectory.FromString("Content/"));
            EntitiesActiveEndpoint = URLAddress.FromString($"{ContentBaseUrl.Value}ActiveEntitiesByPointer.json")!;
            AssetBundleRegistry = URLAddress.FromString($"{ContentBaseUrl.Value}ActiveEntitiesByPointer.json")!;
            LambdasBaseUrl = URLDomain.EMPTY;
        }

        public UniTask PublishAsync<T>(EntityDefinitionGeneric<T> entity, CancellationToken ct, IReadOnlyDictionary<string, byte[]>? contentFiles = null) =>
            throw new NotSupportedException();

        public string GetFileHash(byte[] file) =>
            file.IpfsHashV1();
    }
}
