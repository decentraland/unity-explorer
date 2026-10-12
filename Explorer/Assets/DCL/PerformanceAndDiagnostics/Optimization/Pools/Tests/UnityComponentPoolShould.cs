using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DCL.Optimization.Pools.Tests
{
    [TestFixture]
    public class UnityComponentPoolShould
    {
        [SetUp]
        public void SetUp()
        {
            onGetCallCount = 0;
            gameObjectPool = new GameObjectPool<Transform>(null, null, null, 1000, _ => onGetCallCount++);
        }

        [TearDown]
        public void TearDown()
        {
            gameObjectPool.Clear();
        }

        private GameObjectPool<Transform> gameObjectPool = null!;
        private int onGetCallCount;

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

        [Test]
        public void SkipDestroyedObjectAndReturnValidComponent()
        {
            // Arrange: place a component in the pool, then destroy its GameObject while it sits inactive
            Transform pooled = gameObjectPool.Get();
            gameObjectPool.Release(pooled);
            Assert.AreEqual(1, gameObjectPool.CountInactive);

            Object.DestroyImmediate(pooled.gameObject);

            // Act: Get() must skip the destroyed entry (logging the error is expected) and return a live component
            LogAssert.Expect(LogType.Error, "Transform has been destroyed while in the pool.");
            Transform result = gameObjectPool.Get();

            // Assert
            Assert.NotNull(result);
            Assert.IsTrue(result.gameObject.activeSelf);

            // onGet callback must have fired for the valid component (twice total: first Get + this Get)
            Assert.AreEqual(2, onGetCallCount);

            gameObjectPool.Release(result);
        }

        [Test]
        public void RestoreCreationTransformOnRelease()
        {
            // Arrange: a prefab-like creation with a non-default root transform
            var creationPosition = new Vector3(1, 2, 3);
            Quaternion creationRotation = Quaternion.Euler(10, 20, 30);
            var creationScale = new Vector3(2, 2, 2);

            var pool = new GameObjectPool<Transform>(null, () =>
            {
                Transform created = new GameObject().transform;
                created.SetLocalPositionAndRotation(creationPosition, creationRotation);
                created.localScale = creationScale;
                return created;
            });

            Transform pooled = pool.Get();
            var scaledParent = new GameObject();
            scaledParent.transform.localScale = new Vector3(128, 40, 128);
            pooled.SetParent(scaledParent.transform, worldPositionStays: true);
            pooled.SetLocalPositionAndRotation(Vector3.one * 50, Quaternion.Euler(45, 45, 45));

            //Act
            pool.Release(pooled);
            Transform reused = pool.Get();

            //Assert
            Assert.AreSame(pooled, reused);
            Assert.AreEqual(creationPosition, reused.localPosition);
            Assert.AreEqual(0f, Quaternion.Angle(creationRotation, reused.localRotation), 1e-3f);
            Assert.AreEqual(creationScale, reused.localScale);

            pool.Release(reused);
            pool.Clear();
            Object.DestroyImmediate(pool.ParentContainer.gameObject);
            Object.DestroyImmediate(scaledParent);
        }
    }
}
