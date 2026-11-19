using Arch.Core;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.System;
using ECS.Abstract;
using ECS.TestSuite;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Visibility.Components;
using ECS.Unity.Visibility.Systems;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace ECS.Unity.Visibility.Tests
{
    public class VisibilityTextShapeSystemShould : UnitySystemTestBase<VisibilityTextShapeSystem>
    {
        private TextMeshPro text = null!;
        private GameObject parentObject = null!;

        [SetUp]
        public void SetUp()
        {
            text = new GameObject("Text").AddComponent<TextMeshPro>();
            parentObject = new GameObject("Parent");
            system = new VisibilityTextShapeSystem(world, new EntityEventBuffer<TextShapeComponent>(1));
        }

        [Test]
        public void KeepInheritedVisibilityAcrossFramesAndParentChanges()
        {
            // Arrange
            var parentTransform = new TransformComponent(parentObject.transform);
            var visibility = new PBVisibilityComponent { Visible = false, PropagateToChildren = true, IsDirty = true };
            Entity parent = world.Create(parentTransform, visibility);
            Entity child = world.Create(new TextShapeComponent(text) { IsContainedInScene = true },
                new TransformComponent(text.transform) { Parent = parent });
            parentTransform.Children.Add(child);
            text.transform.SetParent(parentObject.transform);
            using var propagationSystem = new VisibilityPropagationSystem(world);

            foreach (bool visible in new[] { false, true, false })
            {
                // Act
                visibility.Visible = visible;
                visibility.IsDirty = true;
                propagationSystem.Update(0);
                system.Update(0);

                // Assert
                Assert.That(text.enabled, Is.EqualTo(visible));
                Assert.That(world.Has<PBVisibilityComponent>(child), Is.False);

                // Act
                visibility.IsDirty = false;
                world.Get<ResolvedVisibilityComponent>(child).IsDirty = false;
                system.Update(0);

                // Assert
                Assert.That(text.enabled, Is.EqualTo(visible), "Visibility must persist after dirty flags are cleared.");
            }
        }

        [Test]
        public void RespectResolvedVisibilityWhenBoundsChange([Values] bool visible, [Values] bool hasOwnVisibility)
        {
            // Arrange
            Entity entity = world.Create(new TextShapeComponent(text),
                new ResolvedVisibilityComponent { IsVisible = visible, IsDirty = false });
            if (hasOwnVisibility)
                world.Add(entity, new PBVisibilityComponent { Visible = !visible });

            foreach (bool contained in new[] { true, false, true })
            {
                // Act
                world.Get<TextShapeComponent>(entity).IsContainedInScene = contained;
                system.Update(0);

                // Assert
                Assert.That(text.enabled, Is.EqualTo(visible && contained));
            }
        }

        [Test]
        public void RespectVisibilityWithoutResolvedComponent([Values] bool hasOwnVisibility, [Values] bool visible)
        {
            // Arrange
            Entity entity = world.Create(new TextShapeComponent(text));
            if (hasOwnVisibility)
                world.Add(entity, new PBVisibilityComponent { Visible = visible });

            foreach (bool contained in new[] { true, false, true })
            {
                // Act
                world.Get<TextShapeComponent>(entity).IsContainedInScene = contained;
                system.Update(0);

                // Assert
                Assert.That(text.enabled, Is.EqualTo(contained && (!hasOwnVisibility || visible)));
            }
        }

        protected override void OnTearDown()
        {
            Object.DestroyImmediate(text.gameObject);
            Object.DestroyImmediate(parentObject);
        }
    }
}
