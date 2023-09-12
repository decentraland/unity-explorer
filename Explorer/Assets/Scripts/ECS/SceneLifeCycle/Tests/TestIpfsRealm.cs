using CommunicationData.URLHelpers;
using Ipfs;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.SceneLifeCycle.Tests
{
    public class TestIpfsRealm : IIpfsRealm
    {
        public TestIpfsRealm(string[] sceneUrns = null)
        {
            SceneUrns = sceneUrns ?? Array.Empty<string>();
            CatalystBaseUrl = URLDomain.FromString($"file://{Application.dataPath + "/../TestResources/"}");
            ContentBaseUrl = CatalystBaseUrl.Append(URLSubdirectory.FromString("Content/"));
            EntitiesActiveEndpoint = URLDomain.FromString($"{ContentBaseUrl.Value}ActiveEntitiesByPointer.json");
        }

        public IReadOnlyList<string> SceneUrns { get; }
        public URLDomain EntitiesActiveEndpoint { get; }

        public URLDomain CatalystBaseUrl { get; }
        public URLDomain ContentBaseUrl { get; }
    }
}
