using Arch.Core;
using Arch.Core.Utils;
using CRDT;
using DCL.ECSComponents;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.TestSuite;
// using DCL.Optimization.Pools;
using ECS.Unity.AvatarShape.Systems;
using ECS.Unity.Transforms.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;

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

            PBAvatarShape pbAvatarShapeComponent = new PBAvatarShape() { Name = "cthulhu"};
            world.Add(entity, pbAvatarShapeComponent);

            system.Update(0);

            Assert.AreEqual(1, globalWorld.CountEntities(new QueryDescription().WithAll<PBAvatarShape>()));
            world.Query(new QueryDescription().WithAll<PBAvatarShape>(), (ref PBAvatarShape comp) => Assert.AreEqual(pbAvatarShapeComponent, comp));
        }

        [Test]
        public void ForwardSDKAvatarShapeUpdateToGlobalWorldSystems()
        {

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
