using System;
using UnityEngine;
using Utility;

namespace DCL.PerformanceAndDiagnostics.Optimization.Pools
{
    public class GameObjectPoolDCL<T> : IComponentPoolDCL<T> where T: Component
    {
        private readonly string DEFAULT_COMPONENT_NAME = $"POOL_OBJECT_{typeof(T).Name}";
        private readonly ObjectPoolDCL<T> gameObjectPool;
        private readonly Transform parentContainer;
        private readonly Transform rootContainer;

        private readonly Action<T> onRelease;

        public int CountInactive => gameObjectPool.CountInactive;

        public GameObjectPoolDCL(Transform rootContainer, Func<T> creationHandler = null, Action<T> onRelease = null, int maxSize = 2048)
        {
            parentContainer = new GameObject($"POOL_CONTAINER_{typeof(T).Name}").transform;
            parentContainer.SetParent(rootContainer);
            this.onRelease += onRelease;
            gameObjectPool = new ObjectPoolDCL<T>(creationHandler ?? HandleCreation, actionOnGet: HandleGet, actionOnRelease: HandleRelease, actionOnDestroy: UnityObjectUtils.SafeDestroyGameObject, defaultCapacity: maxSize / 4, maxSize: maxSize);
        }

        public void Dispose() =>
            Clear();

        public PooledObjectDCL<T> Get(out T v) =>
            gameObjectPool.Get(out v);

        public void Release(T element) =>
            gameObjectPool.Release(element);

        public T Get() =>
            gameObjectPool.Get();

        public void Clear() =>
            gameObjectPool.Clear();

        public void Clear(int maxChunkSize) =>
            gameObjectPool.Clear(maxChunkSize);

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
            if (component == null || UnityObjectUtils.IsQuitting)
                return;

            onRelease?.Invoke(component);

            GameObject gameObject;
            (gameObject = component.gameObject).SetActive(false);
            gameObject.name = DEFAULT_COMPONENT_NAME;
            component.gameObject.transform.SetParent(parentContainer);
        }
    }
}
