using Arch.Core;
using DCL.ECSComponents;
using ECS.ComponentsPooling;
using ECS.TestSuite;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.Systems;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
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
                { PBMeshRenderer.MeshOneofCase.Box, Substitute.For<ISetupMesh>() },
                { PBMeshRenderer.MeshOneofCase.Sphere, Substitute.For<ISetupMesh>() },
                { PBMeshRenderer.MeshOneofCase.Cylinder, Substitute.For<ISetupMesh>() },
                { PBMeshRenderer.MeshOneofCase.Plane, Substitute.For<ISetupMesh>() },
            };

        private Entity entity;

        [SetUp]
        public void SetUp()
        {
            poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(MeshRenderer), new UnityComponentPool<MeshRenderer>(null, MeshRendererPoolUtils.CreateMeshRendererComponent, MeshRendererPoolUtils.ReleaseMeshRendererComponent) },
                    { typeof(Mesh), new ComponentPool<Mesh>(null, mesh => mesh.Clear()) },
                });

            system = new InstantiatePrimitiveRenderingSystem(world, poolsRegistry, setupMeshes);

            entity = world.Create();
            AddTransformToEntity(entity);
        }

        [Test]
        [TestCaseSource(nameof(TestCases))]
        public void InstantiateNonExistingRenderer(PBMeshRenderer input, PBMeshRenderer.MeshOneofCase expectedType)
        {
            world.Add(entity, input);

            system.Update(0);

            ref PrimitiveRendererComponent meshRenderer = ref world.Get<PrimitiveRendererComponent>(entity);
            ref PrimitiveMeshComponent meshComponent = ref world.Get<PrimitiveMeshComponent>(entity);

            Assert.AreEqual(expectedType, meshComponent.SDKType);
            setupMeshes[input.MeshCase].Received(1).Execute(input, meshComponent.Mesh);

            Assert.AreEqual(meshRenderer.MeshRenderer.GetComponent<MeshFilter>().sharedMesh, meshComponent.Mesh);
        }

        [Test]
        [TestCaseSource(nameof(TestCases))]
        public void UpdateInvalidatedRenderer(PBMeshRenderer input, PBMeshRenderer.MeshOneofCase expectedType)
        {
            input.IsDirty = true;

            world.Add(entity, input);

            var previousComponent = new PrimitiveMeshComponent { Mesh = null, SDKType = PBMeshRenderer.MeshOneofCase.None };
            world.Add(entity, previousComponent);

            system.Update(0);

            ref PrimitiveRendererComponent meshRenderer = ref world.Get<PrimitiveRendererComponent>(entity);
            ref PrimitiveMeshComponent meshComponent = ref world.Get<PrimitiveMeshComponent>(entity);

            Assert.AreEqual(expectedType, meshComponent.SDKType);
            setupMeshes[input.MeshCase].Received(1).Execute(input, meshComponent.Mesh);

            Assert.AreEqual(meshRenderer.MeshRenderer.GetComponent<MeshFilter>().sharedMesh, meshComponent.Mesh);
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
