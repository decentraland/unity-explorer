using NUnit.Framework;
using UnityEngine;

namespace ECS.Unity.Components.Tests
{
    [TestFixture]
    public class UnityTransformHandlerShould
    {
        private UnityTransformHandler handler;

        [SetUp]
        public void Setup()
        {
            handler = new UnityTransformHandler();
        }

        [Test]
        public void CreateANewTransform()
        {
            // Act
            Transform transform = handler.HandleCreation();

            // Assert
            Assert.IsNotNull(transform);
            Assert.AreEqual(handler.parentContainer, transform.parent);
            Assert.IsFalse(transform.gameObject.activeSelf);
            Assert.AreEqual(handler.defaultName, transform.gameObject.name);
        }

        [Test]
        public void GetTransform()
        {
            // Arrange
            Transform transform = handler.HandleCreation();

            // Act
            handler.HandleGet(transform);

            // Assert
            Assert.IsTrue(transform.gameObject.activeSelf);
        }

        [Test]
        public void ReleaseTransform()
        {
            // Arrange
            Transform transform = handler.HandleCreation();
            transform.gameObject.SetActive(true);
            transform.gameObject.name = "SomeCustomName";

            // Act
            handler.HandleRelease(transform);

            // Assert
            Assert.IsFalse(transform.gameObject.activeSelf);
            Assert.AreEqual(handler.defaultName, transform.gameObject.name);
            Assert.AreEqual(handler.parentContainer, transform.parent);
        }
    }
}
