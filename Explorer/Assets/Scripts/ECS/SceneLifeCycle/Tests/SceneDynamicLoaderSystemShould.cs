using CrdtEcsBridge.Components.Special;
using Cysharp.Threading.Tasks;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace ECS.SceneLifeCycle.Systems.Tests
{
    [TestFixture]
    public class SceneDynamicLoaderSystemShould : UnitySystemTestBase<SceneDynamicLoaderSystem>
    {
        [SetUp]
        public void SetUp()
        {
            system = new SceneDynamicLoaderSystem(world, new SceneLifeCycleState()
            {
                ScenePointers = new Dictionary<Vector2Int, Ipfs.SceneEntityDefinition>(),
                SceneLoadRadius = 2,
                PlayerEntity = world.Create(new PlayerComponent(), new TransformComponent())
            });
        }

        [Test]
        public async Task LoadScenePointers() {
            // should start the WebRequest
            system.Update(0.0f);
            Assert.IsTrue(system.pointerRequest.HasValue);
            var (request, _) = system.pointerRequest.Value;

            // wait until the request is done
            await request;

            // should process the WebRequest, and load the scenes
            system.Update(0.0f);

            foreach (var (position, hash) in system.state.ScenePointers)
            {
                Debug.Log($"Position {position.x} {position.y} = {hash}");
            }
        }

        [TearDown]
        public void TearDown()
        {
        }
    }
}
