using NUnit.Framework;
using UnityEngine;

namespace ECS.ComponentsPooling.Tests
{
    [TestFixture]
    public class UnityGameObjectPoolShould
    {
        private UnityGameObjectPool unityComponentPool;


        [SetUp]
        public void SetUp()
        {
            unityComponentPool = new UnityGameObjectPool();
        }

        [Test]
        public void GetGameObject()
        {
            //Act
            unityComponentPool.Get(out GameObject gameObject);

            //Assert
            Assert.NotNull(gameObject);
            Assert.IsTrue(gameObject.activeSelf);
            Assert.AreEqual(1, unityComponentPool.CountActive);
        }

        [Test]
        public void ReleaseGameObject()
        {
            //Act
            unityComponentPool.Get(out GameObject gameObject);
            unityComponentPool.Release(gameObject);

            //Assert
            Assert.NotNull(gameObject);
            Assert.IsFalse(gameObject.activeSelf);
            Assert.AreEqual(1, unityComponentPool.CountInactive);
        }

        [Test]
        public void ClearPool()
        {
            //Act
            unityComponentPool.Get(out GameObject gameObject);
            unityComponentPool.Release(gameObject);
            unityComponentPool.Clear();

            //Assert
            Assert.IsTrue(gameObject == null);
            Assert.AreEqual(0, unityComponentPool.CountInactive);
        }

        [TearDown]
        public void TearDown()
        {
            unityComponentPool.Clear();
        }
    }
}
