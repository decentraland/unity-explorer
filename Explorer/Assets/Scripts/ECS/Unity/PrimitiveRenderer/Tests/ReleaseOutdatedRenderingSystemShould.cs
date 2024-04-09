using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.TestSuite;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
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


        public void SetUp()
        {
            poolsRegistry = Substitute.For<IComponentPoolsRegistry>();
            system = new ReleaseOutdatedRenderingSystem(world, poolsRegistry);
        }


        public void InvalidateRenderingIfTypeChanged()
        {
            var comp = new PrimitiveMeshRendererComponent
            {
                PrimitiveMesh = new BoxPrimitive(),
                SDKType = PBMeshRenderer.MeshOneofCase.Box,
            };

            var sdkComp = new PBMeshRenderer { Sphere = new PBMeshRenderer.Types.SphereMesh(), IsDirty = true };

            Entity entity = world.Create(comp, sdkComp);

            system.Update(0);

            poolsRegistry.Received(1).TryGetPool(typeof(BoxPrimitive), out Arg.Any<IComponentPool>());

            Assert.AreEqual(null, world.Get<PrimitiveMeshRendererComponent>(entity).PrimitiveMesh);
        }


        public void ReleaseRendererIfComponentRemoved()
        {
            var comp = new PrimitiveMeshRendererComponent
            {
                PrimitiveMesh = new BoxPrimitive(),
                SDKType = PBMeshRenderer.MeshOneofCase.Box,
            };

            Entity entity = world.Create(comp);

            system.Update(0);

            poolsRegistry.Received(1).TryGetPool(typeof(BoxPrimitive), out Arg.Any<IComponentPool>());
            Assert.That(world.Has<PrimitiveMeshRendererComponent>(entity), Is.False);
        }


        public void DoNothingIfNotDirty()
        {
            var oldRenderer = new Mesh();
            BoxFactory.Create(ref oldRenderer);

            var comp = new PrimitiveMeshRendererComponent
            {
                PrimitiveMesh = new BoxPrimitive(),
                SDKType = PBMeshRenderer.MeshOneofCase.Box,
            };

            var sdkComp = new PBMeshRenderer { Sphere = new PBMeshRenderer.Types.SphereMesh(), IsDirty = false };

            Entity entity = world.Create(comp, sdkComp);

            system.Update(0);

            poolsRegistry.DidNotReceive().TryGetPool(Arg.Any<Type>(), out Arg.Any<IComponentPool>());

            Assert.AreEqual(comp.PrimitiveMesh, world.Get<PrimitiveMeshRendererComponent>(entity).PrimitiveMesh);
        }
    }
}
