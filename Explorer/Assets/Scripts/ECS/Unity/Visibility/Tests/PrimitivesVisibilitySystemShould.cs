using Arch.Core;
using DCL.ECSComponents;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.Visibility.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ECS.Unity.Visibility.Tests
{
    public class PrimitivesVisibilitySystemShould : UnitySystemTestBase<PrimitivesVisibilitySystem>
    {

        public void SetUp()
        {
            system = new PrimitivesVisibilitySystem(world);
        }


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


        public void ChangeVisibilityWhenComponentRemoved()
        {
            // Arrange
            MeshRenderer renderer = new GameObject().AddComponent<MeshRenderer>();

            Entity e = world.Create(new PBVisibilityComponent
                { Visible = false, IsDirty = true }, new PBMeshRenderer
                { IsDirty = true }, new PrimitiveMeshRendererComponent { MeshRenderer = renderer }, RemovedComponents.CreateDefault());

            system.Update(0);
            Assert.That(renderer.enabled, Is.EqualTo(false));

            // Act
            world.Remove<PBVisibilityComponent>(e);
            world.Get<RemovedComponents>(e).Set.Add(typeof(PBVisibilityComponent));
            system.Update(0);

            //Assert
            Assert.That(renderer.enabled, Is.EqualTo(true));

            // Act
            world.Add(e, new PBVisibilityComponent
                { Visible = false, IsDirty = true });

            world.Get<PBMeshRenderer>(e).IsDirty = true;
            system.Update(0);

            //Assert
            Assert.That(renderer.enabled, Is.EqualTo(false));
        }
    }
}
