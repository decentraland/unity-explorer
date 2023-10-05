using NUnit.Framework;
using UnityEngine;

namespace ECS.ComponentsPooling.Tests
{
    [TestFixture]
    public class UnityComponentPoolShould
    {
        private GameObjectPool<Transform> gameObjectPool;

        [SetUp]
        public void SetUp()
        {
            gameObjectPool = new GameObjectPool<Transform>(null, null, null, 1000);
        }

        [Test]
        public void GetGameObject()
        {
            //Act
            gameObjectPool.Get(out Transform component);

            //Assert
            Assert.NotNull(component);
            Assert.IsTrue(component.gameObject.activeSelf);
        }

        [Test]
        public void ReleaseGameObject()
        {
            //Act
            gameObjectPool.Get(out Transform component);
            gameObjectPool.Release(component);

            //Assert
            Assert.NotNull(component);
            Assert.IsFalse(component.gameObject.activeSelf);
            Assert.AreEqual(1, gameObjectPool.CountInactive);
        }

        [Test]
        public void ClearPool()
        {
            //Act
            gameObjectPool.Get(out Transform component);
            gameObjectPool.Release(component);
            gameObjectPool.Clear();

            //Assert
            Assert.IsTrue(component == null);
            Assert.AreEqual(0, gameObjectPool.CountInactive);
        }

        [TearDown]
        public void TearDown()
        {
            gameObjectPool.Clear();
        }
    }
}
