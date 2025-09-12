using Arch.Core;
using ECS.TestSuite;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using ECS.Unity.PrimitiveRenderer.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Rendering;
using Utility.Primitives;
using System.Collections.Generic;
using DCL.ECSComponents;
using ECS.Unity.GltfNodeModifiers.Components;

namespace ECS.Unity.Materials.Tests
{
    public class ResetMaterialSystemShould : UnitySystemTestBase<ResetMaterialSystem>
    {
        private DestroyMaterial destroyMaterial;

        private Entity entity;
        private MeshRenderer renderer;

        [SetUp]
        public void SetUp()
        {
            system = new ResetMaterialSystem(world, destroyMaterial = Substitute.For<DestroyMaterial>(), Substitute.For<ISceneData>());

            Material dm = DefaultMaterial.New();

            renderer = new GameObject(nameof(ResetMaterialSystemShould)).AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.sharedMaterial = dm;

            var matComp = new MaterialComponent { Status = StreamableLoading.LifeCycle.Applied, Result = dm };
            var rendComp = new PrimitiveMeshRendererComponent { MeshRenderer = renderer };

            entity = world.Create(matComp, rendComp);
        }

        [Test]
        public void SetDefaultMaterial()
        {
            Material mat = DefaultMaterial.Get();
            DefaultMaterial.Release(mat);

            system.Update(0);

            Assert.That(renderer.sharedMaterial, Is.EqualTo(mat));
        }

        [Test]
        public void DereferenceMaterial()
        {
            system.Update(0);

            destroyMaterial.Received(1)(in Arg.Any<MaterialData>(), Arg.Any<Material>());
        }

        [Test]
        public void DeleteComponent()
        {
            system.Update(0);

            Assert.That(world.Has<MaterialComponent>(entity), Is.False);
        }

        [Test]
        public void ResetGltfNodeMaterial()
        {
            // Arrange
            var originalMaterial = new Material(DefaultMaterial.Get());
            var newMaterial = new Material(DefaultMaterial.Get());
            var testGameObject = new GameObject("TestRenderer");
            var meshRenderer = testGameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = newMaterial;

            // Create container entity with GltfNodeModifiers containing original materials
            var containerEntity = world.Create();
            var gltfNodeModifiers = new GltfNodeModifiers.Components.GltfNodeModifiers(
                new Dictionary<Entity, string>(), 
                new Dictionary<Renderer, Material> { { meshRenderer, originalMaterial } }
            );
            world.Add(containerEntity, gltfNodeModifiers);

            // Create GltfNode entity with MaterialComponent and PBMaterial
            var materialComponent = new MaterialComponent
            {
                Result = newMaterial,
                Status = StreamableLoading.LifeCycle.Applied
            };

            var gltfNode = new GltfNode(new[] { meshRenderer }, containerEntity, "TestPath", true);

            var gltfNodeEntity = world.Create(gltfNode, materialComponent, new PBMaterial
            {
                Pbr = new PBMaterial.Types.PbrMaterial
                {
                    AlbedoColor = new Decentraland.Common.Color4 { R = 1f, G = 0f, B = 0f, A = 1f }
                }
            });

            // Verify initial state
            Assert.AreEqual(newMaterial, meshRenderer.sharedMaterial);

            // Act - Remove PBMaterial to trigger reset (this simulates what GltfNodeModifier systems do)
            world.Remove<PBMaterial>(gltfNodeEntity);
            system.Update(0);

            // Assert - Original material should be restored and entity should be destroyed
            Assert.AreEqual(originalMaterial, meshRenderer.sharedMaterial);
            Assert.That(world.IsAlive(gltfNodeEntity), Is.False); // Entity should be destroyed since CleanupDestruction = true

            // Cleanup
            Object.DestroyImmediate(testGameObject);
            Object.DestroyImmediate(originalMaterial);
            Object.DestroyImmediate(newMaterial);
        }
    }
}
