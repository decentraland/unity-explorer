using Arch.Core;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.Optimization.Pools;
using ECS.TestSuite;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.PrimitiveColliders.Systems;
using ECS.Unity.SceneBoundsChecker;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;

namespace ECS.Unity.PrimitiveColliders.Tests
{
    public class ReleaseOutdatedColliderSystemShould : UnitySystemTestBase<ReleaseOutdatedColliderSystem>
    {
        private IComponentPoolsRegistry poolsRegistry;
        private IEntityCollidersSceneCache collidersSceneCache;

        [SetUp]
        public void SetUp()
        {
            poolsRegistry = Substitute.For<IComponentPoolsRegistry>();
            system = new ReleaseOutdatedColliderSystem(world, poolsRegistry, collidersSceneCache = Substitute.For<IEntityCollidersSceneCache>());
        }

        [Test]
        public void InvalidateColliderIfTypeChanged()
        {
            BoxCollider oldCollider = new GameObject().AddComponent<BoxCollider>();

            var comp = new PrimitiveColliderComponent(new SDKCollider(oldCollider), typeof(BoxCollider), PBMeshCollider.MeshOneofCase.Box);

            var sdkComp = new PBMeshCollider { Sphere = new PBMeshCollider.Types.SphereMesh(), IsDirty = true };

            Entity entity = world.Create(comp, sdkComp);

            system.Update(0);

            poolsRegistry.Received(1).TryGetPool(typeof(BoxCollider), out Arg.Any<IComponentPool>());
            collidersSceneCache.Received(1).Remove(Arg.Any<Collider>());

            Assert.AreEqual(null, world.Get<PrimitiveColliderComponent>(entity).Collider);
        }

        [Test]
        public void ReleaseColliderIfComponentRemoved()
        {
            BoxCollider oldCollider = new GameObject().AddComponent<BoxCollider>();

            var comp = new PrimitiveColliderComponent(new SDKCollider(oldCollider), typeof(BoxCollider), PBMeshCollider.MeshOneofCase.Box);

            // No SDK component attached
            Entity entity = world.Create(comp);

            system.Update(0);

            poolsRegistry.Received(1).TryGetPool(typeof(BoxCollider), out Arg.Any<IComponentPool>());
            collidersSceneCache.Received(1).Remove(Arg.Any<Collider>());
            Assert.That(world.Has<PrimitiveColliderComponent>(entity), Is.False);
        }

        [Test]
        public void DoNothingIfNotDirty()
        {
            BoxCollider oldCollider = new GameObject().AddComponent<BoxCollider>();
            var comp = new PrimitiveColliderComponent(new SDKCollider(oldCollider), typeof(BoxCollider), PBMeshCollider.MeshOneofCase.Box);

            var sdkComp = new PBMeshCollider { Sphere = new PBMeshCollider.Types.SphereMesh(), IsDirty = false };

            Entity entity = world.Create(comp, sdkComp);

            system.Update(0);

            poolsRegistry.DidNotReceive().TryGetPool(Arg.Any<Type>(), out Arg.Any<IComponentPool>());
            collidersSceneCache.DidNotReceive().Remove(Arg.Any<Collider>());

            Assert.AreEqual(comp.Collider, world.Get<PrimitiveColliderComponent>(entity).Collider);
        }
    }
}
