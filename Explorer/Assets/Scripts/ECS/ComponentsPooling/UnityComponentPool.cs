using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace ECS.ComponentsPooling
{
    public class UnityComponentPool<T> : IComponentPool<T> where T: Component
    {
        private readonly string DEFAULT_COMPONENT_NAME = $"POOL_OBJECT_{typeof(T).Name}";
        private readonly ObjectPool<T> gameObjectPool;
        private readonly Transform parentContainer;
        private readonly Transform rootContainer;

        public UnityComponentPool(Transform rootContainer, int maxSize = 2048)
        {
            parentContainer = new GameObject($"POOL_CONTAINER_{typeof(T).Name}").transform;
            parentContainer.SetParent(rootContainer);
            gameObjectPool = new ObjectPool<T>(HandleCreation, actionOnGet: HandleGet, actionOnRelease: HandleRelease, actionOnDestroy: HandleDestroy, defaultCapacity: maxSize / 4, maxSize: maxSize);
        }

        public PooledObject<T> Get(out T v) =>
            gameObjectPool.Get(out v);

        public void Release(T element) =>
            gameObjectPool.Release(element);

        public T Get() =>
            gameObjectPool.Get();

        public void Clear() =>
            gameObjectPool.Clear();

        public int CountInactive => gameObjectPool.CountInactive;

        public void Dispose() =>
            Clear();

        private T HandleCreation()
        {
            var go = new GameObject(DEFAULT_COMPONENT_NAME);
            go.gameObject.SetActive(false);
            return go.TryAddComponent<T>();
        }

        private void HandleGet(T component)
        {
            component.gameObject.SetActive(true);
        }

        private void HandleRelease(T component)
        {
            component.gameObject.SetActive(false);
            component.gameObject.name = DEFAULT_COMPONENT_NAME;
            component.gameObject.transform.SetParent(parentContainer);
        }

        private void HandleDestroy(T component)
        {
#if UNITY_EDITOR
            Object.DestroyImmediate(component.gameObject);
#else
            GameObject.Destroy(component.gameObject);
#endif
        }
    }
}
