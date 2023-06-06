using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace ECS.LifeCycle.Components
{
    public struct RemovedComponents : IDisposable
    {
        public readonly HashSet<Type> RemovedComponentsSet;

        private RemovedComponents(HashSet<Type> defaultHashSet)
        {
            RemovedComponentsSet = defaultHashSet;
        }

        public static RemovedComponents CreateDefault() =>
            new (HashSetPool<Type>.Get());

        public void Dispose() =>
            HashSetPool<Type>.Release(RemovedComponentsSet);
    }
}
