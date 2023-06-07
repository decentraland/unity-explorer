using System;
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

        private readonly Action<T> onRelease;

        public UnityComponentPool(Transform rootContainer, Func<T> creationHandler = null, Action<T> onRelease = null, int maxSize = 2048)
        {
            parentContainer = new GameObject($"POOL_CONTAINER_{typeof(T).Name}").transform;
            parentContainer.SetParent(rootContainer);
            this.onRelease += onRelease;
            gameObjectPool = new ObjectPool<T>(creationHandler ?? HandleCreation, actionOnGet: HandleGet, actionOnRelease: HandleRelease, actionOnDestroy: UnityObjectUtils.SafeDestroyGameObject, defaultCapacity: maxSize / 4, maxSize: maxSize);
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
            if (component == null)
                return;

            onRelease?.Invoke(component);

            GameObject gameObject;
            (gameObject = component.gameObject).SetActive(false);
            gameObject.name = DEFAULT_COMPONENT_NAME;
            component.gameObject.transform.SetParent(parentContainer);
        }
    }
}
