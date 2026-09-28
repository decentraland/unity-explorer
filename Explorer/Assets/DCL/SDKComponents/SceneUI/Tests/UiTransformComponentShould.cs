using CRDT;
using DCL.SDKComponents.SceneUI.Components;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Tests
{
    /// <summary>
    ///     Root and child components share one pool and a released root keeps its flag, so each initializer must
    ///     resolve <see cref="UITransformComponent.Transform" /> against its own role.
    /// </summary>
    public class UiTransformComponentShould
    {
        [Test]
        public void UseTheReusableElementWhenAFormerRootIsInitializedAsChild()
        {
            // Arrange
            var component = new UITransformComponent();
            var root = new VisualElement { name = "root" };
            component.InitializeAsRoot(root);

            // Act
            component.InitializeAsChild("UITransform", new CRDTEntity(7), new CRDTEntity(0));

            // Assert
            Assert.That(component.IsRoot, Is.False);
            Assert.That(component.Transform, Is.Not.SameAs(root));
            Assert.That(component.Transform.name, Is.Not.Empty);
            Assert.That(component.Transform.userData, Is.SameAs(component));
            Assert.That(root.name, Is.EqualTo("root"));
        }

        [Test]
        public void ReplaceTheRootElementWhenAFormerRootIsInitializedAsRootAgain()
        {
            // Arrange
            var component = new UITransformComponent();
            component.InitializeAsRoot(new VisualElement());
            var newRoot = new VisualElement();

            // Act
            component.InitializeAsRoot(newRoot);

            // Assert
            Assert.That(component.IsRoot, Is.True);
            Assert.That(component.Transform, Is.SameAs(newRoot));
        }

        [Test]
        public void UseTheRootElementWhenAFormerChildIsInitializedAsRoot()
        {
            // Arrange
            var component = new UITransformComponent();
            component.InitializeAsChild("UITransform", new CRDTEntity(7), new CRDTEntity(0));
            VisualElement child = component.Transform;
            var root = new VisualElement();

            // Act
            component.InitializeAsRoot(root);

            // Assert
            Assert.That(component.IsRoot, Is.True);
            Assert.That(component.Transform, Is.SameAs(root));
            Assert.That(component.Transform, Is.Not.SameAs(child));
        }
    }
}
