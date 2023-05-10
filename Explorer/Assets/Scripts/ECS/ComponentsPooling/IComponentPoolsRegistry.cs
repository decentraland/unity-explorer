using Google.Protobuf;
using System;

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
        /// Get or Create the message pool of the protobuf message.
        /// It is guaranteed that no special logic for Protobuf messages is required
        /// so the pool can be instantiated lazily
        /// </summary>
        /// <typeparam name="T">Protobuf Message</typeparam>
        /// <returns></returns>
        IComponentPool<T> GetReferenceTypePool<T>() where T: class, new();

        /// <summary>
        /// <inheritdoc cref="GetReferenceTypePool{T}"/>
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        IComponentPool GetReferenceTypePool(Type type);
    }
}
