using Arch.Core;
using DCL.LOD;
using DCL.Roads.Components;
using DCL.Roads.Systems;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Roads.Tests
{
    public class UnloadRoadSystemShould : UnitySystemTestBase<UnloadRoadSystem>
    {
        private IRoadAssetPool roadAssetPool;

        [SetUp]
        public void Setup()
        {
            roadAssetPool =  Substitute.For<IRoadAssetPool>();
            var scenesCache = Substitute.For<IScenesCache>();
            system = new UnloadRoadSystem(world, roadAssetPool, scenesCache);
        }

        [Test]
        public void UnloadRoad()
        {
            // Arrange
            var roadInfo = new RoadInfo
            {
                CurrentKey = "key", CurrentAsset = new GameObject().transform
            };

            SceneLoadingState sceneLoadingState = SceneLoadingState.CreateRoad();
            sceneLoadingState.PromiseCreated = true;

            PartitionComponent partitionComponent  = new PartitionComponent();
            partitionComponent.OutOfRange = true;

            Entity entity = world.Create(roadInfo, new SceneDefinitionComponent(), sceneLoadingState, partitionComponent);

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Get<SceneLoadingState>(entity).PromiseCreated);
            roadAssetPool.Received().Release(Arg.Any<string>(), Arg.Any<Transform>());
        }

    }
}
