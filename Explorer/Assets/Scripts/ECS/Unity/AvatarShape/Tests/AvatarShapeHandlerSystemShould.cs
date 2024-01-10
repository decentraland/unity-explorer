using Arch.Core;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.ECSComponents;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.AvatarShape.Systems;
using NUnit.Framework;
using NSubstitute;

namespace ECS.Unity.AvatarShape.Tests
{
    [TestFixture]
    public class AvatarShapeHandlerSystemShould : UnitySystemTestBase<AvatarShapeHandlerSystem>
    {
        private Entity entity;
        private World globalWorld;

        [SetUp]
        public void SetUp()
        {
            globalWorld = World.Create();
            var worldProxy = new WorldProxy();
            worldProxy.SetWorld(globalWorld);
            system = new AvatarShapeHandlerSystem(world, worldProxy);

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
        }

        [Test]
        public void ForwardSDKAvatarShapeInstantiationToGlobalWorldSystems()
        {
            Assert.AreEqual(0, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>()));

            PBAvatarShape pbAvatarShapeComponent = new PBAvatarShape() { Name = "Cthulhu"};
            world.Add(entity, pbAvatarShapeComponent);

            system.Update(0);

            Assert.AreEqual(1, world.CountEntities(new QueryDescription().WithAll<PBAvatarShape, AvatarShapeComponent>()));
            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>()));
            globalWorld.Query(new QueryDescription().WithAll<PBAvatarShape>(), (ref PBAvatarShape comp) => Assert.AreEqual(pbAvatarShapeComponent.Name, comp.Name));
        }

        [Test]
        public void ForwardSDKAvatarShapeUpdateToGlobalWorldSystems()
        {
            Assert.AreEqual(0, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>()));

            // Creation
            PBAvatarShape pbAvatarShapeComponent = new PBAvatarShape() { Name = "Cthulhu"};
            world.Add(entity, pbAvatarShapeComponent);

            system.Update(0);

            // Update
            pbAvatarShapeComponent.Name = "Dagon";
            world.Add(entity, pbAvatarShapeComponent);

            system.Update(0);

            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>()));
            globalWorld.Query(new QueryDescription().WithAll<PBAvatarShape>(), (ref PBAvatarShape comp) => Assert.AreEqual(pbAvatarShapeComponent.Name, comp.Name));
        }

        [Test]
        public void RemoveEntityFromGlobalWorldOnComponentRemove()
        {

        }

        [Test]
        public void RemoveEntityFromGlobalWorldOnSceneEntityDestruction()
        {

        }
    }
}
