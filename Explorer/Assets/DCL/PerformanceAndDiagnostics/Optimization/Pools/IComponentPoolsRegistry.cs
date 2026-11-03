using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace DCL.Optimization.Pools
{
    /// <summary>
    ///     The registry of the pools of components including both SDK ones and non-SDK ones. <br />
    ///     The set of the pools of components should be provided
    ///     as a shared instance across the worlds and CRDT Deserialization <br />
    ///     Threadsafe
    /// </summary>
    public interface IComponentPoolsRegistry : IDisposable
    {
        /// <summary>
        ///     Gets the pool registered for <paramref name="type" />. Returns false when no pool is registered for that type
        /// </summary>
        bool TryGetPool(Type type, out IComponentPool componentPool);

        bool TryGetPool<T>(out IComponentPool componentPool);

        /// <summary>
        ///     Gets the pool registered for <typeparamref name="T" />. Returns false when no pool is registered for <typeparamref name="T" />,
        ///     including when the pool registered for that type is not an <see cref="IComponentPool{T}" />
        /// </summary>
        /// <typeparam name="T">Any reference type</typeparam>
        bool TryGetReferenceTypePool<T>([MaybeNullWhen(false)] out IComponentPool<T> pool) where T: class;

        GameObjectPool<T> AddGameObjectPool<T>(Func<T> creationHandler = null, Action<T> onRelease = null, int maxSize = 1024, Action<T> onGet = null) where T: Component;

        Transform RootContainerTransform();

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
