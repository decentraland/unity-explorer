using System;
using UnityEngine;

namespace DCL.Optimization.Pools
{
    /// <summary>
    ///     The registry of the pools of components including both SDK ones and non-SDK ones. <br />
    ///     The set of the pools of components should be provided
    ///     as a shared instance across the worlds and CRDT Deserialization <br />
    ///     Thread-safe
    /// </summary>
    public interface IComponentPoolsRegistry : IDisposable
    {
        bool TryGetPool(Type type, out IComponentPool componentPool);

        bool TryGetPool<T>(out IComponentPool componentPool);

        /// <summary>
        ///     Get the message pool of the reference type. Pool must be registered in advance
        /// </summary>
        /// <typeparam name="T">Any reference type</typeparam>
        /// <returns></returns>
        IComponentPool<T> GetReferenceTypePool<T>() where T: class;

        IComponentPool GetPool(Type type);

        void AddGameObjectPool<T>(Func<T> creationHandler = null, Action<T> onRelease = null, int maxSize = 1024, Action<T> onGet = null) where T: Component;

        void AddComponentPool<T>(IComponentPool<T> componentPool) where T: class;
    }

    public static class ComponentPoolsRegistryExtensions
    {
        public static IComponentPool<T> AddComponentPool<T>(this IComponentPoolsRegistry componentPoolsRegistry, Action<T> onGet = null, Action<T> onRelease = null, int maxSize = 10000) where T: class, new()
        {
            var componentPool = new ComponentPool.WithDefaultCtor<T>(onGet, onRelease, maxSize: maxSize);
            componentPoolsRegistry.AddComponentPool(componentPool);
            return componentPool;
        }
    }
}
