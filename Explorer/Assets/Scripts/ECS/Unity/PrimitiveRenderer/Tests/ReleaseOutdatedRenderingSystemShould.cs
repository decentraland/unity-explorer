using Arch.Core;
using DCL.ECSComponents;
using ECS.ComponentsPooling;
using ECS.TestSuite;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.Systems;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.PrimitiveRenderer.Tests
{
    public class ReleaseOutdatedRenderingSystemShould : UnitySystemTestBase<ReleaseOutdatedRenderingSystem>
    {
        private IComponentPoolsRegistry poolsRegistry;

        [SetUp]
        public void SetUp()
        {
            poolsRegistry = Substitute.For<IComponentPoolsRegistry>();
            system = new ReleaseOutdatedRenderingSystem(world, poolsRegistry);
        }

        [Test]
        public void InvalidateColliderIfTypeChanged()
        {
            var oldRenderer = new Mesh();
            BoxFactory.Create(ref oldRenderer);

            var comp = new PrimitiveMeshComponent
            {
                Mesh = oldRenderer,
                SDKType = PBMeshRenderer.MeshOneofCase.Box,
            };

            var sdkComp = new PBMeshRenderer { Sphere = new PBMeshRenderer.Types.SphereMesh(), IsDirty = true };

            Entity entity = world.Create(comp, sdkComp);

            system.Update(0);

            poolsRegistry.Received(1).TryGetPool(typeof(Mesh), out Arg.Any<IComponentPool>());

            Assert.AreEqual(null, world.Get<PrimitiveMeshComponent>(entity).Mesh);
        }

        [Test]
        public void DoNothingIfNotDirty()
        {
            var oldRenderer = new Mesh();
            BoxFactory.Create(ref oldRenderer);

            var comp = new PrimitiveMeshComponent
            {
                Mesh = oldRenderer,
                SDKType = PBMeshRenderer.MeshOneofCase.Box,
            };

            var sdkComp = new PBMeshRenderer { Sphere = new PBMeshRenderer.Types.SphereMesh(), IsDirty = false };

            Entity entity = world.Create(comp, sdkComp);

            system.Update(0);

            poolsRegistry.DidNotReceive().TryGetPool(Arg.Any<Type>(), out Arg.Any<IComponentPool>());

            Assert.AreEqual(comp.Mesh, world.Get<PrimitiveMeshComponent>(entity).Mesh);
        }
    }
}
