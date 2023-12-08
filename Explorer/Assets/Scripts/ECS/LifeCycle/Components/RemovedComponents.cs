using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using System;
using System.Collections.Generic;

namespace ECS.LifeCycle.Components
{
    public struct RemovedComponents : IDisposable
    {
        private static readonly ThreadSafeHashSetPool<Type> POOL = new (PoolConstants.SDK_COMPONENT_TYPES_COUNT, PoolConstants.SCENES_COUNT);

        public readonly HashSet<Type> Set;

        private RemovedComponents(HashSet<Type> defaultHashSet)
        {
            Set = defaultHashSet;
        }

        public bool Remove<T>() =>
            Set.Remove(typeof(T));

        public static RemovedComponents CreateDefault() =>
            new (POOL.Get());

        public void Dispose() =>
            POOL.Release(Set);
    }
}
