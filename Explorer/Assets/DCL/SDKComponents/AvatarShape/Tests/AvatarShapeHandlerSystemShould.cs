using Arch.Core;
using DCL.ECSComponents;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.AvatarShape.Components;
using ECS.Unity.AvatarShape.Systems;
using NUnit.Framework;
using DCL.Character.Components;
using DCL.Optimization.Pools;
using NSubstitute;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.Unity.AvatarShape.Tests
{
    [TestFixture]
    public class AvatarShapeHandlerSystemShould : UnitySystemTestBase<AvatarShapeHandlerSystem>
    {
        [SetUp]
        public void SetUp()
        {
            globalWorld = World.Create();
            IComponentPool<Transform> pool = Substitute.For<IComponentPool<Transform>>();
            pool.Get().Returns(new GameObject().transform);
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneLoadingConcluded.Returns(true);
            system = new AvatarShapeHandlerSystem(world, globalWorld, pool, sceneData);

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
        }

        private Entity entity;
        private World globalWorld;

        [Test]
        public void ForwardSDKAvatarShapeInstantiationToGlobalWorldSystems()
        {
            Assert.AreEqual(0, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>()));

            var pbAvatarShapeComponent = new PBAvatarShape
                { Name = "Cthulhu" };

            world.Add(entity, pbAvatarShapeComponent);

            system.Update(0);

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<PBAvatarShape, SDKAvatarShapeComponent>()));
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>()));
            globalWorld.Query(new QueryDescription().WithAll<PBAvatarShape, CharacterTransform>(), (ref PBAvatarShape comp) => Assert.AreEqual(pbAvatarShapeComponent.Name, comp.Name));
        }

        [Test]
        public void ForwardSDKAvatarShapeUpdateToGlobalWorldSystems()
        {
            // Creation
            var pbAvatarShapeComponent = new PBAvatarShape
                { Name = "Cthulhu" };

            world.Add(entity, pbAvatarShapeComponent);

            system.Update(0);

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<PBAvatarShape, SDKAvatarShapeComponent>()));
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>()));

            // Update
            pbAvatarShapeComponent.Name = "Dagon";
            world.Set(entity, pbAvatarShapeComponent);

            system.Update(0);

            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>()));
            globalWorld.Query(new QueryDescription().WithAll<PBAvatarShape>(), (ref PBAvatarShape comp) => Assert.AreEqual(pbAvatarShapeComponent.Name, comp.Name));
        }

        [Test]
        public void RemoveEntityFromGlobalWorldOnComponentRemove()
        {
            // Create
            var pbAvatarShapeComponent = new PBAvatarShape
                { Name = "Cthulhu" };

            world.Add(entity, pbAvatarShapeComponent);

            system.Update(0);

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<PBAvatarShape, SDKAvatarShapeComponent>()));
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>().WithNone<DeleteEntityIntention>()));
            Assert.AreEqual(0, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape, DeleteEntityIntention>()));

            // Remove
            world.Remove<PBAvatarShape>(entity);

            system.Update(0);

            Assert.AreEqual(0, world.CountEntities(new QueryDescription().WithAll<PBAvatarShape, SDKAvatarShapeComponent>()));
            Assert.AreEqual(0, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>().WithNone<DeleteEntityIntention>()));
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape, DeleteEntityIntention>()));
        }

        [Test]
        public void RemoveEntityFromGlobalWorldOnSceneEntityDestruction()
        {
            // Create
            var pbAvatarShapeComponent = new PBAvatarShape
                { Name = "Cthulhu" };

            world.Add(entity, pbAvatarShapeComponent);

            system.Update(0);

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<PBAvatarShape, SDKAvatarShapeComponent>()));
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>().WithNone<DeleteEntityIntention>()));
            Assert.AreEqual(0, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape, DeleteEntityIntention>()));

            // Remove
            world.Add<DeleteEntityIntention>(entity);

            system.Update(0);

            Assert.AreEqual(0, world.CountEntities(new QueryDescription().WithAll<PBAvatarShape, SDKAvatarShapeComponent>()));
            Assert.AreEqual(0, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>().WithNone<DeleteEntityIntention>()));
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape, DeleteEntityIntention>()));
        }
    }
}
