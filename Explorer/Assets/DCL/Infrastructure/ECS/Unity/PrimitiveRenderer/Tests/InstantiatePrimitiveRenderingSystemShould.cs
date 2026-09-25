using Arch.Core;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.TestSuite;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using ECS.Unity.PrimitiveRenderer.MeshSetup;
using ECS.Unity.PrimitiveRenderer.Systems;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace ECS.Unity.PrimitiveRenderer.Tests
{
    public class InstantiatePrimitiveRenderingSystemShould : UnitySystemTestBase<InstantiatePrimitiveRenderingSystem>
    {
        private readonly Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> setupMeshes
            = new ()
            {
                { PBMeshRenderer.MeshOneofCase.Box, CreateSubstitute<BoxPrimitive>() },
                { PBMeshRenderer.MeshOneofCase.Sphere, CreateSubstitute<SpherePrimitive>() },
                { PBMeshRenderer.MeshOneofCase.Cylinder, CreateSubstitute<CylinderPrimitive>() },
                { PBMeshRenderer.MeshOneofCase.Plane, CreateSubstitute<PlanePrimitive>() },
            };
        private IComponentPoolsRegistry poolsRegistry;

        private Entity entity;

        private static ISetupMesh CreateSubstitute<T>() where T: IPrimitiveMesh
        {
            ISetupMesh s = Substitute.For<ISetupMesh>();
            s.MeshType.Returns(typeof(T));
            return s;
        }

        [SetUp]
        public void SetUp()
        {
            poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(MeshRenderer), new GameObjectPool<MeshRenderer>(null, MeshRendererPoolUtils.CreateMeshRendererComponent, MeshRendererPoolUtils.ReleaseMeshRendererComponent) },
                    { typeof(BoxPrimitive), new ComponentPool.WithDefaultCtor<BoxPrimitive>() },
                    { typeof(SpherePrimitive), new ComponentPool.WithDefaultCtor<SpherePrimitive>() },
                    { typeof(CylinderPrimitive), new ComponentPool.WithDefaultCtor<CylinderPrimitive>() },
                    { typeof(PlanePrimitive), new ComponentPool.WithDefaultCtor<PlanePrimitive>() },
                }, new GameObject().transform);

            system = CreateSystem(setupMeshes);

            entity = world.Create();
            AddTransformToEntity(entity);
        }

        private InstantiatePrimitiveRenderingSystem CreateSystem(Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> setupMeshCases)
        {
            IReleasablePerformanceBudget budget = Substitute.For<IReleasablePerformanceBudget>();
            budget.TrySpendBudget().Returns(true);

            var buffer = new EntityEventBuffer<PrimitiveMeshRendererComponent>(10);

            return new InstantiatePrimitiveRenderingSystem(world, poolsRegistry, budget, Substitute.For<ISceneData>(), buffer, setupMeshCases);
        }

        [Test]
        [TestCaseSource(nameof(TestCases))]
        public void InstantiateNonExistingRenderer(PBMeshRenderer input, PBMeshRenderer.MeshOneofCase expectedType)
        {
            //Arrange
            world.Add(entity, input);
            system.Update(0);

            //Act
            ref PrimitiveMeshRendererComponent meshRendererComponent = ref world.Get<PrimitiveMeshRendererComponent>(entity);

            //Assert
            Assert.AreEqual(expectedType, meshRendererComponent.SDKType);
            setupMeshes[input.MeshCase].Received(1).Execute(input, meshRendererComponent.PrimitiveMesh);

            Assert.AreEqual(meshRendererComponent.MeshRenderer.GetComponent<MeshFilter>().sharedMesh,
                meshRendererComponent.PrimitiveMesh.Mesh);
        }

        [Test]
        [TestCaseSource(nameof(TestCases))]
        public void UpdateInvalidatedRenderer(PBMeshRenderer input, PBMeshRenderer.MeshOneofCase expectedType)
        {
            //Arrange
            world.Add(entity, input);
            system.Update(0);

            //Act
            // Clear the recorded calls so the assertion below counts only the Execute triggered by re-instantiation
            setupMeshes[input.MeshCase].ClearReceivedCalls();

            input.IsDirty = true;
            world.Get<PrimitiveMeshRendererComponent>(entity).PrimitiveMesh = null;
            system.Update(0);

            //Assert
            ref PrimitiveMeshRendererComponent meshRendererComponent = ref world.Get<PrimitiveMeshRendererComponent>(entity);

            Assert.AreEqual(expectedType, meshRendererComponent.SDKType);
            setupMeshes[input.MeshCase].Received(1).Execute(input, meshRendererComponent.PrimitiveMesh);

            Assert.AreEqual(meshRendererComponent.MeshRenderer.GetComponent<MeshFilter>().sharedMesh,
                meshRendererComponent.PrimitiveMesh.Mesh);
        }

        [Test]
        public void SwapBoxMeshOnFilterWhenUVsChangeAtRuntime()
        {
            //Arrange
            // The substituted setups never swap the mesh, so use the real box setup
            system.Dispose();
            system = CreateSystem(new Dictionary<PBMeshRenderer.MeshOneofCase, ISetupMesh> { { PBMeshRenderer.MeshOneofCase.Box, new MeshSetupBox() } });

            var input = new PBMeshRenderer { Box = new PBMeshRenderer.Types.BoxMesh() };
            world.Add(entity, input);
            system.Update(0);

            MeshFilter meshFilter = world.Get<PrimitiveMeshRendererComponent>(entity).MeshRenderer.GetComponent<MeshFilter>();
            Mesh sharedMesh = meshFilter.sharedMesh;

            //Act
            for (var i = 0; i < 48; i++)
                input.Box.Uvs.Add(0.5f);

            input.IsDirty = true;
            system.Update(0);

            //Assert
            Mesh customMesh = world.Get<PrimitiveMeshRendererComponent>(entity).PrimitiveMesh.Mesh;
            Assert.AreNotSame(sharedMesh, customMesh);
            Assert.AreSame(customMesh, meshFilter.sharedMesh);

            //Act
            input.Box.Uvs.Clear();
            input.IsDirty = true;
            system.Update(0);

            //Assert
            Assert.AreSame(sharedMesh, meshFilter.sharedMesh);
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
