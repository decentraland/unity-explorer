using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.ComponentsPooling
{
    public class ComponentPoolsRegistry : IComponentPoolsRegistry
    {
        private readonly Dictionary<Type, IComponentPool> pools;
        private readonly Transform rootContainer;

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

        public IComponentPool<T> GetReferenceTypePool<T>() where T: class
        {
            lock (pools) { return (IComponentPool<T>)pools[typeof(T)]; }
        }

        public IComponentPool GetPool(Type type)
        {
            lock (pools) { return pools[type]; }
        }

        public void AddGameObjectPool<T>(Func<T> creationHandler = null, Action<T> onRelease = null, int maxSize = 1024) where T: Component
        {
            lock (pools)
            {
                if (pools.ContainsKey(typeof(T)))
                {
                    ReportHub.LogError("ComponentPoolsRegistry", $"Pool for type {typeof(T)} already exists!");
                    return;
                }

                pools.Add(typeof(T), new GameObjectPool<T>(rootContainer, creationHandler, onRelease, maxSize: maxSize));
            }
        }

        public void AddComponentPool<T>(Action<T> onGet = null, Action<T> onRelease = null) where T: class, new()
        {
            lock (pools)
            {
                if (pools.ContainsKey(typeof(T)))
                {
                    ReportHub.LogError("ComponentPoolsRegistry", $"Pool for type {typeof(T)} already exists!");
                    return;
                }

                pools.Add(typeof(T), new ComponentPool<T>(onGet, onRelease));
            }
        }
    }
}
