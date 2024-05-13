using DCL.LOD;
using DCL.Roads.Components;
using DCL.Roads.Systems;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
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
                IsDirty = false, CurrentKey = "key", CurrentAsset = new GameObject().transform
            };
            var entity = world.Create(roadInfo, new VisualSceneState(), new DeleteEntityIntention());

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(world.Has<RoadInfo>(entity));
            roadAssetPool.Received().Release(Arg.Any<string>(), Arg.Any<Transform>());
        }
    }
}