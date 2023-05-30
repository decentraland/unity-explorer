using NUnit.Framework;
using UnityEngine;

namespace ECS.ComponentsPooling.Tests
{
    [TestFixture]
    public class UnityComponentPoolShould
    {
        private UnityComponentPool<Transform> unityComponentPool;

        [SetUp]
        public void SetUp()
        {
            unityComponentPool = new UnityComponentPool<Transform>(null, null, null, 1000);
        }

        [Test]
        public void GetGameObject()
        {
            //Act
            unityComponentPool.Get(out Transform component);

            //Assert
            Assert.NotNull(component);
            Assert.IsTrue(component.gameObject.activeSelf);
        }

        [Test]
        public void ReleaseGameObject()
        {
            //Act
            unityComponentPool.Get(out Transform component);
            unityComponentPool.Release(component);

            //Assert
            Assert.NotNull(component);
            Assert.IsFalse(component.gameObject.activeSelf);
            Assert.AreEqual(1, unityComponentPool.CountInactive);
        }

        [Test]
        public void ClearPool()
        {
            //Act
            unityComponentPool.Get(out Transform component);
            unityComponentPool.Release(component);
            unityComponentPool.Clear();

            //Assert
            Assert.IsTrue(component == null);
            Assert.AreEqual(0, unityComponentPool.CountInactive);
        }

        [TearDown]
        public void TearDown()
        {
            unityComponentPool.Clear();
        }
    }
}
