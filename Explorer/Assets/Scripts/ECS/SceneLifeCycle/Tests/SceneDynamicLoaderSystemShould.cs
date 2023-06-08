using Arch.Core;
using CrdtEcsBridge.Components.Special;
using Cysharp.Threading.Tasks;
using ECS.SceneLifeCycle.Systems;
using ECS.TestSuite;
using Ipfs;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.SceneLifeCycle.Tests
{
    public class TestIpfsRealm : IIpfsRealm
    {
        public TestIpfsRealm()
        {
            CatalystBaseUrl = $"file://{Application.streamingAssetsPath}/";
            ContentBaseUrl = CatalystBaseUrl + "Content/";
        }

        public UnityWebRequestAsyncOperation RequestActiveEntitiesByPointers(List<Vector2Int> pointers)
        {
            var fullPath = $"{ContentBaseUrl}ActiveEntitiesByPointer.json";
            var request = UnityWebRequest.Get(fullPath);
            return request.SendWebRequest();
        }

        public string CatalystBaseUrl { get; }
        public string ContentBaseUrl { get; }
    }

    [TestFixture]
    public class SceneDynamicLoaderSystemShould : UnitySystemTestBase<LoadScenesDynamicallySystem>
    {
        [SetUp]
        public void SetUp()
        {
            var ipfsRealm = new TestIpfsRealm();

            Entity playerEntity = world.Create(new PlayerComponent());
            AddTransformToEntity(playerEntity);

            system = new LoadScenesDynamicallySystem(world, ipfsRealm, new SceneLifeCycleState
            {
                SceneLoadRadius = 2,
                PlayerEntity = playerEntity,
            });
        }

        [TearDown]
        public void TearDown() { }

        [Test]
        public async Task LoadScenePointers()
        {
            // should start the WebRequest
            system.Update(0.0f);
            Assert.IsNotNull(system.pointerRequest);

            // wait until the request is done
            await system.pointerRequest;

            // should process the WebRequest, and load the scenes
            system.Update(0.0f);

            Assert.IsTrue(system.state.ScenePointers.Count == 19);

            HashSet<string> requiredScenes = new ();

            foreach (ScenePointer pointer in system.state.ScenePointers.Values)
                if (!pointer.Definition.id.StartsWith("empty-parcel"))
                    requiredScenes.Add(pointer.Definition.id);

            Assert.IsTrue(requiredScenes.Count == 3);
        }
    }
}
