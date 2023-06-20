using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace ECS.LifeCycle.Components
{
    public struct RemovedComponents : IDisposable
    {
        public readonly HashSet<Type> Set;

        private RemovedComponents(HashSet<Type> defaultHashSet)
        {
            Set = defaultHashSet;
        }

        public bool Remove<T>() =>
            Set.Remove(typeof(T));

        public static RemovedComponents CreateDefault() =>
            new (HashSetPool<Type>.Get());

        public void Dispose() =>
            HashSetPool<Type>.Release(Set);
    }
}
