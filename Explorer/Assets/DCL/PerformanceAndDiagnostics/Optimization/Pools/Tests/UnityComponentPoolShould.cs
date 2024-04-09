using NUnit.Framework;
using UnityEngine;

namespace DCL.Optimization.Pools.Tests
{

    public class UnityComponentPoolShould
    {

        public void SetUp()
        {
            gameObjectPool = new GameObjectPool<Transform>(null, null, null, 1000);
        }


        public void TearDown()
        {
            gameObjectPool.Clear();
        }

        private GameObjectPool<Transform> gameObjectPool;


        public void GetGameObject()
        {
            //Act
            gameObjectPool.Get(out Transform component);

            //Assert
            Assert.NotNull(component);
            Assert.IsTrue(component.gameObject.activeSelf);
        }


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
    }
}
