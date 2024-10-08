using DCL.Diagnostics;
using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Pool;
using Utility;

namespace DCL.Optimization.Pools
{
    public class GameObjectPool<T> : IComponentPool<T> where T: Component
    {
        private static readonly string DEFAULT_COMPONENT_NAME = $"POOL_OBJECT_{typeof(T).Name}";

        private readonly IExtendedObjectPool<T> gameObjectPool;
        private readonly Transform parentContainer;

        private readonly Action<T>? onRelease;
        private readonly Action<T>? onGet;

        public int CountInactive => gameObjectPool.CountInactive;

        public Transform PoolContainerTransform => parentContainer;

        public GameObjectPool(Transform rootContainer, Func<T>? creationHandler = null, Action<T>? onRelease = null, int maxSize = 2048, Action<T>? onGet = null)
        {
            parentContainer = new GameObject($"POOL_CONTAINER_{typeof(T).Name}").transform;
            parentContainer.SetParent(rootContainer);
            if (onRelease != null) this.onRelease += onRelease;
            if (onGet != null) this.onGet += onGet;
            gameObjectPool = new ExtendedObjectPool<T>(creationHandler ?? HandleCreation, actionOnGet: HandleGet, actionOnRelease: HandleRelease, actionOnDestroy: UnityObjectUtils.SafeDestroyGameObject, defaultCapacity: maxSize / 4, maxSize: maxSize);
        }

        public void Dispose() =>
            Clear();

        public PooledObject<T> Get(out T v) =>
            gameObjectPool.Get(out v);

        public void Release(T element)
        {
            // If Application is quitting game objects can be already destroyed
            if (UnityObjectUtils.IsQuitting) return;

            if (element == null)
            {
                ReportHub.LogError(ReportCategory.ENGINE, $"Trying to release `null` reference of type {typeof(T).Name} to the pool");
                return;
            }

            gameObjectPool.Release(element);
        }

        public T Get() =>
            gameObjectPool.Get();

        public void Clear() =>
            gameObjectPool.Clear();

        public void ClearThrottled(int maxChunkSize) =>
            gameObjectPool.ClearThrottled(maxChunkSize);

        private static T HandleCreation()
        {
            var go = new GameObject(DEFAULT_COMPONENT_NAME);
            go.gameObject.SetActive(false);
            return go.TryAddComponent<T>();
        }

        private void HandleGet(T component)
        {
            if (UnityObjectUtils.IsQuitting)
            {
                ReportHub.LogError(ReportCategory.ENGINE, $"Trying to get a component {typeof(T).Name} from a pool while quitting!");
                return;
            }

            component.gameObject.SetActive(true);
            onGet?.Invoke(component);
        }

        private void HandleRelease(T component)
        {
            if (component == null || UnityObjectUtils.IsQuitting)
                return;

            onRelease?.Invoke(component);

            GameObject gameObject;
            (gameObject = component.gameObject).SetActive(false);
            gameObject.name = DEFAULT_COMPONENT_NAME;
            component.gameObject.transform.SetParent(parentContainer, false);
        }
    }
}
