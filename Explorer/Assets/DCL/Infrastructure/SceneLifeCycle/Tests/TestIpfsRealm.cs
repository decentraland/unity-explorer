using CommunicationData.URLHelpers;
using DCL.Ipfs;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SceneLifeCycle.Tests
{
    public class TestIpfsRealm : IIpfsRealm
    {
        public URLDomain LambdasBaseUrl { get; }
        public IReadOnlyList<string> SceneUrns { get; }
        public URLDomain EntitiesActiveEndpoint { get; }
        public URLDomain AssetBundleRegistryEntitiesActive { get; }
        public URLDomain EntitiesBaseUrl => throw new NotSupportedException();

        public URLDomain CatalystBaseUrl { get; }
        public URLDomain ContentBaseUrl { get; }

        public TestIpfsRealm(string[] sceneUrns = null)
        {
            SceneUrns = sceneUrns ?? Array.Empty<string>();
            CatalystBaseUrl = URLDomain.FromString($"file://{Application.dataPath + "/../TestResources/"}");
            ContentBaseUrl = CatalystBaseUrl.Append(URLSubdirectory.FromString("Content/"));
            EntitiesActiveEndpoint = URLDomain.FromString($"{ContentBaseUrl.Value}ActiveEntitiesByPointer.json");
            AssetBundleRegistryEntitiesActive = URLDomain.FromString($"{ContentBaseUrl.Value}ActiveEntitiesByPointer.json");
            LambdasBaseUrl = URLDomain.EMPTY;
        }

        public string GetFileHash(byte[] file) =>
            file.IpfsHashV1();
    }
}
