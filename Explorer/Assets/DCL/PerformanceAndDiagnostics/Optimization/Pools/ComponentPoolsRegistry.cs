using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Optimization.Pools
{
    public class ComponentPoolsRegistry : IComponentPoolsRegistry
    {
        private readonly Dictionary<Type, IComponentPool> pools;
        private readonly Transform rootContainer;

        public ComponentPoolsRegistry() : this(
            new Dictionary<Type, IComponentPool>(),
            new GameObject(nameof(ComponentPoolsRegistry)).transform
        ) { }

        public ComponentPoolsRegistry(Dictionary<Type, IComponentPool> pools, Transform rootContainer)
        {
            this.pools = pools;
            this.rootContainer = rootContainer;
        }

        public void Dispose()
        {
            lock (pools)
            {
                foreach (IComponentPool pool in pools.Values)
                    pool.Dispose();

                pools.Clear();
            }
        }

        public bool TryGetPool(Type type, out IComponentPool componentPool)
        {
            lock (pools) { return pools.TryGetValue(type, out componentPool); }
        }

        public bool TryGetPool<T>(out IComponentPool componentPool)
        {
            lock (pools) { return pools.TryGetValue(typeof(T), out componentPool); }
        }

        public IComponentPool<T> GetReferenceTypePool<T>() where T: class
        {
            lock (pools) { return (IComponentPool<T>)pools[typeof(T)]; }
        }

        public IComponentPool GetPool(Type type)
        {
            lock (pools) { return pools[type]; }
        }

        public IComponentPool<T> AddGameObjectPool<T>(Func<T> creationHandler = null, Action<T> onRelease = null, int maxSize = 1024, Action<T> onGet = null) where T: Component
        {
            lock (pools)
            {
                if (pools.TryGetValue(typeof(T), out var existingPool))
                {
                    ReportHub.LogError("ComponentPoolsRegistry", $"Pool for type {typeof(T)} already exists!");
                    return (IComponentPool<T>) existingPool;
                }

                var newPool = new GameObjectPool<T>(rootContainer, creationHandler, onRelease, maxSize, onGet);
                pools.Add(typeof(T), newPool);
                return newPool;
            }
        }

        public void AddComponentPool<T>(IComponentPool<T> componentPool) where T: class
        {
            lock (pools)
            {
                if (pools.ContainsKey(typeof(T)))
                {
                    ReportHub.LogError("ComponentPoolsRegistry", $"Pool for type {typeof(T)} already exists!");
                    return;
                }

                pools.Add(typeof(T), componentPool);
            }
        }
    }
}
