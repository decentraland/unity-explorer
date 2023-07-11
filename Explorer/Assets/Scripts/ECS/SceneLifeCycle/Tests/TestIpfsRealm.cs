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
            CatalystBaseUrl = $"file://{Application.dataPath + "/../TestResources/"}";
            ContentBaseUrl = CatalystBaseUrl + "Content/";
            EntitiesActiveEndpoint = $"{ContentBaseUrl}ActiveEntitiesByPointer.json";
        }

        public IReadOnlyList<string> SceneUrns { get; }
        public string EntitiesActiveEndpoint { get; }

        public string CatalystBaseUrl { get; }
        public string ContentBaseUrl { get; }
    }
}
