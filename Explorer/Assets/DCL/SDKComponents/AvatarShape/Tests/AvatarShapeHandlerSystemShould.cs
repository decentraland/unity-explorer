using Arch.Core;
using DCL.ECSComponents;
using DCL.Utilities;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.AvatarShape.Components;
using ECS.Unity.AvatarShape.Systems;
using NUnit.Framework;

namespace ECS.Unity.AvatarShape.Tests
{
    [TestFixture]
    public class AvatarShapeHandlerSystemShould : UnitySystemTestBase<AvatarShapeHandlerSystem>
    {
        [SetUp]
        public void SetUp()
        {
            globalWorld = World.Create();
            var globalWorldProxy = new ObjectProxy<World>();
            globalWorldProxy.SetObject(globalWorld);
            system = new AvatarShapeHandlerSystem(world, globalWorldProxy);

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
            globalWorld.Query(new QueryDescription().WithAll<PBAvatarShape>(), (ref PBAvatarShape comp) => Assert.AreEqual(pbAvatarShapeComponent.Name, comp.Name));
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
