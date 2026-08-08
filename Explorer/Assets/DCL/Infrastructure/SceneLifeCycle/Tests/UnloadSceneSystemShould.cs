using Arch.Core;
using DCL.Ipfs;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.SceneLifeCycle.Tests
{
    public class UnloadSceneSystemShould : UnitySystemTestBase<UnloadSceneSystem>
    {
        [SetUp]
        public void SetUp()
        {
            system = new UnloadSceneSystem(world, Substitute.For<IScenesCache>());
        }

        [Test]
        public void DisposeLoadedScene()
        {
            ISceneFacade scene = Substitute.For<ISceneFacade>();

            var definitionComponent = new SceneDefinitionComponent(new SceneEntityDefinition("test-scene", new SceneMetadata()),
                new List<Vector2Int>(), new List<ParcelMathHelper.ParcelCorners>(),
                default(ParcelMathHelper.SceneGeometry), default(IpfsPath), isSDK7: true, isPortableExperience: false);

            Entity e = world.Create(scene, new DeleteEntityIntention(), definitionComponent);

            system!.Update(0f);

            scene.Received(1).DisposeAsync();

            // remain scene definition
            Assert.That(world.GetArchetype(e).Signature, Is.EqualTo(new Signature(typeof(SceneDefinitionComponent))));
        }
    }
}
