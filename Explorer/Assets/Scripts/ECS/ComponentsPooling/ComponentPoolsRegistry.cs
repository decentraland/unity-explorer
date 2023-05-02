using System;
using System.Collections.Generic;

namespace ECS.ComponentsPooling
{
    public class ComponentPoolsRegistry : IComponentPoolsRegistry
    {
        private readonly Dictionary<Type, IComponentPool> pools;

        private ComponentPoolsRegistry(Dictionary<Type, IComponentPool> pools)
        {
            this.pools = pools;
        }

        public bool TryGetPool(Type type, out IComponentPool componentPool)
        {
            lock (pools) { return pools.TryGetValue(type, out componentPool); }
        }

        public IComponentPool<T> GetReferenceTypePool<T>() where T: class, new()
        {
            lock (pools) { return (IComponentPool<T>)(pools.TryGetValue(typeof(T), out var pool) ? pool : pools[typeof(T)] = new ComponentPool<T>()); }
        }

        public IComponentPool GetReferenceTypePool(Type type)
        {
            lock (pools)
            {
                // TODO avoid Activator.CreateInstance
                return pools.TryGetValue(type, out var pool) ? pool : pools[type] = (IComponentPool)Activator.CreateInstance(typeof(ComponentPool<>).MakeGenericType(type));
            }
        }

        public void Dispose()
        {
            lock (pools)
            {
                foreach (var pool in pools.Values)
                    pool.Dispose();

                pools.Clear();
            }
        }
    }
}
