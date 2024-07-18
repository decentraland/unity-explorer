using Arch.Core;
using CRDT;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.Optimization.Pools;
using ECS.TestSuite;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.PrimitiveColliders.Systems;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.PrimitiveColliders.Tests
{
    public class InstantiatePrimitiveColliderSystemShould : UnitySystemTestBase<InstantiatePrimitiveColliderSystem>
    {
        private readonly Dictionary<PBMeshCollider.MeshOneofCase, ISetupCollider> setupColliders
            = new ()
            {
                { PBMeshCollider.MeshOneofCase.Box, CreateSubstitute<BoxCollider>() },
                { PBMeshCollider.MeshOneofCase.Sphere, CreateSubstitute<SphereCollider>() },
                { PBMeshCollider.MeshOneofCase.Cylinder, CreateSubstitute<MeshCollider>() },
                { PBMeshCollider.MeshOneofCase.Plane, CreateSubstitute<BoxCollider>() },
            };
        private IComponentPoolsRegistry poolsRegistry;
        private IEntityCollidersSceneCache entityCollidersSceneCache;

        private Entity entity;

        private static ISetupCollider CreateSubstitute<T>() where T: Collider
        {
            ISetupCollider s = Substitute.For<ISetupCollider>();
            s.ColliderType.Returns(typeof(T));
            return s;
        }

        [SetUp]
        public void SetUp()
        {
            poolsRegistry = new ComponentPoolsRegistry(
                new Dictionary<Type, IComponentPool>
                {
                    { typeof(BoxCollider), new GameObjectPool<BoxCollider>(null) },
                    { typeof(MeshCollider), new GameObjectPool<MeshCollider>(null) },
                    { typeof(SphereCollider), new GameObjectPool<SphereCollider>(null) },
                }, new GameObject().transform);

            system = new InstantiatePrimitiveColliderSystem(world, poolsRegistry, entityCollidersSceneCache = Substitute.For<IEntityCollidersSceneCache>(), setupColliders);

            entity = world.Create(new CRDTEntity(5));
            AddTransformToEntity(entity);
        }

        [Test]
        [TestCaseSource(nameof(TestCases))]
        public void InstantiateNonExistingCollider(PBMeshCollider input, Type expectedType)
        {
            world.Add(entity, input);

            system.Update(0);

            ref PrimitiveColliderComponent colliderComp = ref world.Get<PrimitiveColliderComponent>(entity);

            Assert.AreEqual(expectedType, colliderComp.Collider.GetType());
            Assert.AreEqual(expectedType, colliderComp.ColliderType);
            setupColliders[input.MeshCase].Received(1).Execute(colliderComp.Collider, input);
            entityCollidersSceneCache.Received(1).Associate(colliderComp.Collider, Arg.Any<ColliderSceneEntityInfo>());

            Assert.AreEqual(input.MeshCase, colliderComp.SDKType);
        }

        [Test]
        [TestCaseSource(nameof(TestCases))]
        public void UpdateInvalidatedCollider(PBMeshCollider input, Type expectedType)
        {
            input.IsDirty = true;

            world.Add(entity, input);

            var previousComponent = new PrimitiveColliderComponent { Collider = null, ColliderType = typeof(SphereCollider), SDKType = PBMeshCollider.MeshOneofCase.None };
            world.Add(entity, previousComponent);

            system.Update(0);

            ref PrimitiveColliderComponent colliderComp = ref world.Get<PrimitiveColliderComponent>(entity);

            Assert.AreEqual(expectedType, colliderComp.Collider.GetType());
            Assert.AreEqual(expectedType, colliderComp.ColliderType);
            setupColliders[input.MeshCase].Received(1).Execute(colliderComp.Collider, input);
            entityCollidersSceneCache.Received(1).Associate(colliderComp.Collider, Arg.Any<ColliderSceneEntityInfo>());

            Assert.AreEqual(input.MeshCase, colliderComp.SDKType);
        }

        [Test]
        [TestCaseSource(nameof(TestCases))]
        public void UpdateChangedCollider(PBMeshCollider input, Type expectedType)
        {
            world.Add(entity, input);

            system.Update(0);

            foreach (ISetupCollider collider in setupColliders.Values)
                collider.ClearReceivedCalls();

            entityCollidersSceneCache.ClearReceivedCalls();

            input.IsDirty = true;
            system.Update(0);

            ref PrimitiveColliderComponent colliderComp = ref world.Get<PrimitiveColliderComponent>(entity);

            Assert.AreEqual(expectedType, colliderComp.Collider.GetType());
            Assert.AreEqual(expectedType, colliderComp.ColliderType);
            setupColliders[input.MeshCase].Received(1).Execute(colliderComp.Collider, input);
            entityCollidersSceneCache.Received(1).Associate(colliderComp.Collider, Arg.Any<ColliderSceneEntityInfo>());

            Assert.AreEqual(input.MeshCase, colliderComp.SDKType);
        }

        public static object[][] TestCases()
        {
            return new[]
            {
                new object[]
                {
                    new PBMeshCollider { Box = new PBMeshCollider.Types.BoxMesh() },
                    typeof(BoxCollider),
                },
                new object[]
                {
                    new PBMeshCollider { Sphere = new PBMeshCollider.Types.SphereMesh() },
                    typeof(SphereCollider),
                },
                new object[]
                {
                    new PBMeshCollider
                    {
                        Cylinder = new PBMeshCollider.Types.CylinderMesh
                            { RadiusBottom = 0.5f, RadiusTop = 0.5f },
                    },
                    typeof(MeshCollider),
                },
                new object[]
                {
                    new PBMeshCollider
                    {
                        Cylinder = new PBMeshCollider.Types.CylinderMesh
                            { RadiusBottom = 0.25f, RadiusTop = 0.5f },
                    },
                    typeof(MeshCollider),
                },
                new object[]
                {
                    new PBMeshCollider
                    {
                        Cylinder = new PBMeshCollider.Types.CylinderMesh
                            { RadiusBottom = 0.5f, RadiusTop = 0f },
                    },
                    typeof(MeshCollider),
                },
                new object[]
                {
                    new PBMeshCollider { Plane = new PBMeshCollider.Types.PlaneMesh() },
                    typeof(BoxCollider),
                },
            };
        }
    }
}
