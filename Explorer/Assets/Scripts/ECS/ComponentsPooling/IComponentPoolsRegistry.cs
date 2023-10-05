using System;
using UnityEngine;

namespace ECS.ComponentsPooling
{
    /// <summary>
    /// The registry of the pools of components including both SDK ones and non-SDK ones. <br/>
    /// The set of the pools of components should be provided
    /// as a shared instance across the worlds and CRDT Deserialization <br/>
    /// Thread-safe
    /// </summary>
    public interface IComponentPoolsRegistry : IDisposable
    {
        bool TryGetPool(Type type, out IComponentPool componentPool);

        /// <summary>
        /// Get the message pool of the reference type. Pool must be registered in advance
        /// </summary>
        /// <typeparam name="T">Any reference type</typeparam>
        /// <returns></returns>
        IComponentPool<T> GetReferenceTypePool<T>() where T: class;

        IComponentPool GetPool(Type type);

        void AddGameObjectPool<T>(Func<T> creationHandler = null, Action<T> onRelease = null, int maxSize = 1024) where T: Component;

        void AddComponentPool<T>(Action<T> onGet = null, Action<T> onRelease = null) where T: class, new();


    }
}
