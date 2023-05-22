using System;
using System.Collections.Generic;

namespace ECS.ComponentsPooling
{
    public class ComponentPoolsRegistry : IComponentPoolsRegistry
    {
        private readonly Dictionary<Type, IComponentPool> pools;

        public ComponentPoolsRegistry(Dictionary<Type, IComponentPool> pools)
        {
            this.pools = pools;
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

        public void Dispose()
        {
            lock (pools)
            {
                foreach (IComponentPool pool in pools.Values)
                    pool.Dispose();

                pools.Clear();
            }
        }
    }
}
