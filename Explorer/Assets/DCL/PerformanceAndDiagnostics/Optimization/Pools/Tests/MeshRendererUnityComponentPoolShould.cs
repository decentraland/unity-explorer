using Cysharp.Threading.Tasks;
using NUnit.Framework;
using System.Threading.Tasks;
using UnityEngine;
using Utility;
using Utility.Primitives;

namespace DCL.Optimization.Pools.Tests
{

    public class MeshRendererUnityComponentPoolShould
    {

        public void SetUp()
        {
            gameObjectPool = new GameObjectPool<MeshRenderer>(null,
                MeshRendererPoolUtils.CreateMeshRendererComponent, MeshRendererPoolUtils.ReleaseMeshRendererComponent, 1000);

            mesh = new Mesh();
        }


        public void TearDown()
        {
            gameObjectPool.Clear();
        }

        private GameObjectPool<MeshRenderer> gameObjectPool;
        private Mesh mesh;


        public void GetGameObject()
        {
            //Act
            gameObjectPool.Get(out MeshRenderer component);

            //Assert
            Assert.NotNull(component);
            Assert.IsTrue(component.gameObject.activeSelf);
            Assert.NotNull(component.gameObject.GetComponent<MeshFilter>());
        }


        public async Task ReleaseGameObject()
        {
            //Arrange
            BoxFactory.Create(ref mesh);

            //Act
            gameObjectPool.Get(out MeshRenderer component);
            component.material = DefaultMaterial.New();
            gameObjectPool.Release(component);

            await UniTask.Yield(PlayerLoopTiming.Update);

            //Assert
            Assert.NotNull(component);
            Assert.IsFalse(component.gameObject.activeSelf);
            Assert.IsNull(component.sharedMaterial);

            Assert.NotNull(component.gameObject.GetComponent<MeshFilter>());
            Assert.IsNull(component.gameObject.GetComponent<MeshFilter>().sharedMesh);

            Assert.AreEqual(1, gameObjectPool.CountInactive);
        }


        public void ClearPool()
        {
            //Act
            gameObjectPool.Get(out MeshRenderer component);
            gameObjectPool.Release(component);
            gameObjectPool.Clear();

            //Assert
            Assert.IsTrue(component == null);
            Assert.AreEqual(0, gameObjectPool.CountInactive);
        }
    }
}
