using System;
using UnityEngine;
using UnityEngine.Pool;
using Utility;

namespace DCL.Optimization.Pools
{
    public class GameObjectPool<T> : IComponentPool<T> where T: Component
    {
        private static readonly string DEFAULT_COMPONENT_NAME = $"POOL_OBJECT_{typeof(T).Name}";

        public readonly IExtendedObjectPool<T> gameObjectPool;
        private readonly Transform parentContainer;
        private readonly Transform rootContainer;

        private readonly Action<T> onRelease;

        public int CountInactive => gameObjectPool.CountInactive;

        public GameObjectPool(Transform rootContainer, Func<T> creationHandler = null, Action<T> onRelease = null, int maxSize = 2048, bool collectionCheck = true)
        {
            parentContainer = new GameObject($"POOL_CONTAINER_{typeof(T).Name}").transform;
            parentContainer.SetParent(rootContainer);
            this.onRelease += onRelease;
            gameObjectPool = new ExtendedObjectPool<T>(creationHandler ?? HandleCreation, actionOnGet: HandleGet, actionOnRelease: HandleRelease, actionOnDestroy: UnityObjectUtils.SafeDestroyGameObject, defaultCapacity: maxSize / 4, maxSize: maxSize, collectionCheck: collectionCheck);
        }

        public void Dispose() =>
            Clear();

        public PooledObject<T> Get(out T v) =>
            gameObjectPool.Get(out v);

        public void Release(T element) =>
            gameObjectPool.Release(element);

        public T Get() =>
            gameObjectPool.Get();

        public void Clear() =>
            gameObjectPool.Clear();

        public void ClearThrottled(int maxChunkSize) =>
            gameObjectPool.ClearThrottled(maxChunkSize);

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

            OnHandleRelease(component);
        }

        protected virtual void OnHandleRelease(T component)
        {
            GameObject gameObject;
            (gameObject = component.gameObject).SetActive(false);
            gameObject.name = DEFAULT_COMPONENT_NAME;
            component.gameObject.transform.SetParent(parentContainer);
        }
    }
}
