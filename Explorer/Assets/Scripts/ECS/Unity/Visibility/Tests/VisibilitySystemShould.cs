using Arch.Core;
using DCL.ECSComponents;
using ECS.TestSuite;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.Visibility.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ECS.Unity.Visibility.Tests
{
    public class VisibilitySystemShould : UnitySystemTestBase<VisibilitySystem>
    {
        [SetUp]
        public void SetUp()
        {
            system = new VisibilitySystem(world);
        }

        [Test]
        public void ChangeVisibility()
        {
            // Arrange
            bool[] visibilityChanges = { true, false, true };
            MeshRenderer renderer = new GameObject().AddComponent<MeshRenderer>();
            Entity e = world.Create(new PBVisibilityComponent(), new PBMeshRenderer(), new PrimitiveMeshRendererComponent { MeshRenderer = renderer });

            for (var i = 0; i < visibilityChanges.Length; i++)
            {
                // Arrange
                world.Get<PBVisibilityComponent>(e).Visible = visibilityChanges[i];
                world.Get<PBVisibilityComponent>(e).IsDirty = true;
                world.Get<PBMeshRenderer>(e).IsDirty = true;

                // Act
                system.Update(0);

                // Assert
                Assert.That(renderer.enabled, Is.EqualTo(visibilityChanges[i]));
            }
        }
    }
}
