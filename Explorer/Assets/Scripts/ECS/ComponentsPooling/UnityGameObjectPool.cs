using UnityEngine;
using UnityEngine.Pool;

namespace ECS.ComponentsPooling
{
    public class UnityGameObjectPool : IComponentPool<GameObject>
    {
        private readonly ObjectPool<GameObject> gameObjectPool;
        private readonly Transform rootContainer;

        private readonly string DEFAULT_COMPONENT_NAME = "POOL_OBJECT";

        public UnityGameObjectPool(Transform rootContainer = null)
        {
            this.rootContainer = rootContainer;
            gameObjectPool = new ObjectPool<GameObject>(HandleCreation, actionOnGet: HandleGet, actionOnRelease: HandleRelease, actionOnDestroy: HandleDestroy, defaultCapacity: 1000);
        }

        private GameObject HandleCreation()
        {
            var go = new GameObject(DEFAULT_COMPONENT_NAME);
            go.gameObject.SetActive(false);
            return go;
        }

        private void HandleGet(GameObject gameObject)
        {
            gameObject.SetActive(true);
        }

        private void HandleRelease(GameObject gameObject)
        {
            gameObject.SetActive(false);
            gameObject.name = DEFAULT_COMPONENT_NAME;
            gameObject.transform.SetParent(rootContainer);
        }

        private void HandleDestroy(GameObject gameObject)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(gameObject);
#else
                GameObject.Destroy(gameObject);
#endif
        }

        public PooledObject<GameObject> Get(out GameObject v) =>
            gameObjectPool.Get(out v);

        public void Release(GameObject gameObject) =>
            gameObjectPool.Release(gameObject);

        GameObject IObjectPool<GameObject>.Get() =>
            gameObjectPool.Get();

        public void Clear() =>
            gameObjectPool.Clear();

        public int CountInactive => gameObjectPool.CountInactive;
        public int CountActive => gameObjectPool.CountActive;

        public void Dispose() =>
            Clear();
    }
}
