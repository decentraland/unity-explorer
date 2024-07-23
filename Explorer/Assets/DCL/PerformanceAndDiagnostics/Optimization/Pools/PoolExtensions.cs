using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.Optimization.Pools
{
    public static class PoolExtensions
    {
        private class EmptyPool<T> : IObjectPool<T> where T : class, new()
        {
            internal static readonly EmptyPool<T> INSTANCE = new ();

            public T Get() =>
                new ();

            public PooledObject<T> Get(out T v) =>
                throw new NotImplementedException();

            public void Release(T element)
            {

            }

            public void Clear()
            {

            }

            public int CountInactive => 0;
        }

        public static Scope<TElement> EmptyScope<TElement>(TElement defaultValue) where TElement: class, new() =>
            new (defaultValue, EmptyPool<TElement>.INSTANCE);

        public static Scope<List<TComponent>> GetComponentsInChildrenIntoPooledList<TComponent>(this GameObject go, bool includeInactive = false) where TComponent: class
        {
            Scope<List<TComponent>> scope = AutoScope(UnityComponentPool<TComponent>.INSTANCE);
            go.GetComponentsInChildren(includeInactive, scope.Value);
            return scope;
        }

        public static Scope<TElement> AutoScope<TElement>(this IObjectPool<TElement> pool) where TElement: class =>
            new (pool.Get(), pool);

        internal class UnityComponentPool<T> : ListObjectPool<T>
        {
            internal static readonly UnityComponentPool<T> INSTANCE = new ();
        }

        public struct Scope<TElement> : IDisposable where TElement: class
        {
            private readonly IObjectPool<TElement> pool;
            public TElement Value { get; private set; }

            internal Scope(TElement value, IObjectPool<TElement> pool)
            {
                this.pool = pool;
                Value = value;
            }

            public void Dispose()
            {
                pool.Release(Value);
                Value = null;
            }
        }
    }
}
