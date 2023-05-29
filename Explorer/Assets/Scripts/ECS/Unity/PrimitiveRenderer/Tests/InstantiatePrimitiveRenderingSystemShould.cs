using System;
using System.Collections.Generic;
using Arch.Core;
using DCL.ECSComponents;
using ECS.ComponentsPooling;
using ECS.TestSuite;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using ECS.Unity.PrimitiveRenderer.MeshSetup;
using ECS.Unity.PrimitiveRenderer.Systems;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Utility;

namespace ECS.Unity.PrimitiveRenderer.Tests
{
    public class InstantiatePrimitiveRenderingSystemShould : UnitySystemTestBase<InstantiatePrimitiveRenderingSystem>
    {
        private IComponentPoolsRegistry poolsRegistry;

        private readonly Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> setupMeshes
            = new ()
            {
                { PBMeshRenderer.MeshOneofCase.Box, CreateSubstitute<BoxPrimitive>() },
                { PBMeshRenderer.MeshOneofCase.Sphere, CreateSubstitute<SpherePrimitive>() },
                { PBMeshRenderer.MeshOneofCase.Cylinder, CreateSubstitute<CylinderPrimitive>() },
                { PBMeshRenderer.MeshOneofCase.Plane, CreateSubstitute<PlanePrimitive>() }
            };

        private static ISetupMesh CreateSubstitute<T>() where T : IPrimitiveMesh
        {
            var s = Substitute.For<ISetupMesh>();
            s.MeshType.Returns(typeof(T));
            return s;
        }

        private Entity entity;

        [SetUp]
        public void SetUp()
        {
            poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(MeshRenderer), new UnityComponentPool<MeshRenderer>(null, MeshRendererPoolUtils.CreateMeshRendererComponent, MeshRendererPoolUtils.ReleaseMeshRendererComponent) },
                    { typeof(BoxPrimitive), new ComponentPool<BoxPrimitive>() },
                    { typeof(SpherePrimitive), new ComponentPool<SpherePrimitive>() },
                    { typeof(CylinderPrimitive), new ComponentPool<CylinderPrimitive>() },
                    { typeof(PlanePrimitive), new ComponentPool<PlanePrimitive>() }
                });

            system = new InstantiatePrimitiveRenderingSystem(world, poolsRegistry, setupMeshes);

            entity = world.Create();
            AddTransformToEntity(entity);
        }

        [Test]
        [TestCaseSource(nameof(TestCases))]
        public void InstantiateNonExistingRenderer(PBMeshRenderer input, PBMeshRenderer.MeshOneofCase expectedType)
        {
            //Arrange
            world.Add(entity, input);
            system.Update(0);

            //Act
            ref var meshRendererComponent = ref world.Get<PrimitiveMeshRendererComponent>(entity);

            //Assert
            Assert.AreEqual(expectedType, meshRendererComponent.SDKType);
            setupMeshes[input.MeshCase].Received(1).Execute(input, meshRendererComponent.PrimitiveMesh.PrimitiveMesh);

            Assert.AreEqual(meshRendererComponent.MeshRenderer.GetComponent<MeshFilter>().sharedMesh,
                meshRendererComponent.PrimitiveMesh.PrimitiveMesh);
        }

        [Test]
        [TestCaseSource(nameof(TestCases))]
        public void UpdateInvalidatedRenderer(PBMeshRenderer input, PBMeshRenderer.MeshOneofCase expectedType)
        {
            //Arrange
            world.Add(entity, input);
            system.Update(0);

            //Act
            input.IsDirty = true;
            world.Get<PrimitiveMeshRendererComponent>(entity).PrimitiveMesh = null;
            system.Update(0);

            //Assert
            ref var meshRendererComponent = ref world.Get<PrimitiveMeshRendererComponent>(entity);

            Assert.AreEqual(expectedType, meshRendererComponent.SDKType);
            setupMeshes[input.MeshCase].Received(1).Execute(input, meshRendererComponent.PrimitiveMesh.PrimitiveMesh);

            Assert.AreEqual(meshRendererComponent.MeshRenderer.GetComponent<MeshFilter>().sharedMesh,
                meshRendererComponent.PrimitiveMesh.PrimitiveMesh);
        }

        public static object[][] TestCases()
        {
            return new[]
            {
                new object[]
                {
                    new PBMeshRenderer { Box = new PBMeshRenderer.Types.BoxMesh() },
                    PBMeshRenderer.MeshOneofCase.Box,
                },
                new object[]
                {
                    new PBMeshRenderer { Sphere = new PBMeshRenderer.Types.SphereMesh() },
                    PBMeshRenderer.MeshOneofCase.Sphere,
                },
                new object[]
                {
                    new PBMeshRenderer
                    {
                        Cylinder = new PBMeshRenderer.Types.CylinderMesh
                            { RadiusBottom = 0.5f, RadiusTop = 0.5f },
                    },
                    PBMeshRenderer.MeshOneofCase.Cylinder,
                },
                new object[]
                {
                    new PBMeshRenderer
                    {
                        Cylinder = new PBMeshRenderer.Types.CylinderMesh
                            { RadiusBottom = 0.25f, RadiusTop = 0.5f },
                    },
                    PBMeshRenderer.MeshOneofCase.Cylinder,
                },
                new object[]
                {
                    new PBMeshRenderer
                    {
                        Cylinder = new PBMeshRenderer.Types.CylinderMesh
                            { RadiusBottom = 0.5f, RadiusTop = 0f },
                    },
                    PBMeshRenderer.MeshOneofCase.Cylinder,
                },
                new object[]
                {
                    new PBMeshRenderer { Plane = new PBMeshRenderer.Types.PlaneMesh() },
                    PBMeshRenderer.MeshOneofCase.Plane,
                },
            };
        }
    }
}
