using Arch.Core;
using DCL.ECSComponents;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading;
using ECS.TestSuite;
using ECS.Unity.Materials.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.PrimitiveRenderer.MeshPrimitive;
using ECS.Unity.PrimitiveRenderer.Systems;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace ECS.Unity.PrimitiveRenderer.Tests
{
    public class RenderInstancedPrimitivesSystemShould : UnitySystemTestBase<RenderInstancedPrimitivesSystem>
    {
        private readonly List<Object> created = new ();

        private PrimitiveInstanceBatches batches = null!;
        private Material material = null!;

        [SetUp]
        public void SetUp()
        {
            batches = new PrimitiveInstanceBatches();
            system = new RenderInstancedPrimitivesSystem(world, batches);

            material = new Material(Shader.Find("DCL/Universal Render Pipeline/Lit"));
            created.Add(material);
        }

        protected override void OnTearDown()
        {
            foreach (Object obj in created)
                Object.DestroyImmediate(obj);

            created.Clear();
        }

        [Test]
        public void HideRendererAndCollectInstance()
        {
            // Arrange
            Entity entity = CreatePrimitive(LifeCycle.Applied);

            // Act
            system.Update(0);

            // Assert
            PrimitiveMeshRendererComponent component = world.Get<PrimitiveMeshRendererComponent>(entity);
            Assert.IsTrue(component.InstancedRendering);
            Assert.IsTrue(component.MeshRenderer.forceRenderingOff);
            Assert.AreEqual(1, batches.BatchCount);
            Assert.AreEqual(1, batches.InstanceCount(0));
        }

        [Test]
        public void BatchPrimitivesSharingMeshAndMaterial()
        {
            // Arrange
            CreatePrimitive(LifeCycle.Applied);
            CreatePrimitive(LifeCycle.Applied);

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(1, batches.BatchCount);
            Assert.AreEqual(2, batches.InstanceCount(0));
        }

        [Test]
        public void LeaveRendererAloneUntilMaterialIsApplied()
        {
            // Arrange
            Entity entity = CreatePrimitive(LifeCycle.LoadingInProgress);

            // Act
            system.Update(0);

            // Assert
            PrimitiveMeshRendererComponent component = world.Get<PrimitiveMeshRendererComponent>(entity);
            Assert.IsFalse(component.InstancedRendering);
            Assert.IsFalse(component.MeshRenderer.forceRenderingOff);
            Assert.AreEqual(0, batches.BatchCount);
        }

        [Test]
        public void SkipDisabledRendererButKeepItHidden()
        {
            // Arrange
            Entity entity = CreatePrimitive(LifeCycle.Applied);
            world.Get<PrimitiveMeshRendererComponent>(entity).MeshRenderer.enabled = false;

            // Act
            system.Update(0);

            // Assert
            PrimitiveMeshRendererComponent component = world.Get<PrimitiveMeshRendererComponent>(entity);
            Assert.IsTrue(component.MeshRenderer.forceRenderingOff);
            Assert.AreEqual(0, batches.BatchCount);
        }

        [Test]
        public void RestoreRendererWhenMaterialIsRemoved()
        {
            // Arrange
            Entity entity = CreatePrimitive(LifeCycle.Applied);
            system.Update(0);

            // Act
            world.Remove<MaterialComponent>(entity);
            system.Update(0);

            // Assert
            PrimitiveMeshRendererComponent component = world.Get<PrimitiveMeshRendererComponent>(entity);
            Assert.IsFalse(component.InstancedRendering);
            Assert.IsFalse(component.MeshRenderer.forceRenderingOff);
            Assert.AreEqual(0, batches.BatchCount);
        }

        [Test]
        public void SkipEntitiesMarkedForDeletion()
        {
            // Arrange
            Entity entity = CreatePrimitive(LifeCycle.Applied);
            world.Add<DeleteEntityIntention>(entity);

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(0, batches.BatchCount);
        }

        private Entity CreatePrimitive(LifeCycle materialStatus)
        {
            Entity entity = world.Create(new PBMeshRenderer());
            AddTransformToEntity(entity);

            MeshRenderer renderer = MeshRendererPoolUtils.CreateMeshRendererComponent();
            created.Add(renderer.gameObject);

            world.Add(entity, new PrimitiveMeshRendererComponent
            {
                MeshRenderer = renderer,
                PrimitiveMesh = new BoxPrimitive(),
                SDKType = PBMeshRenderer.MeshOneofCase.Box,
            });

            world.Add(entity, new MaterialComponent { Status = materialStatus, Result = material });
            return entity;
        }
    }
}
