using System;
using UnityEngine.Pool;

namespace ECS.ComponentsPooling
{
    public interface IComponentPool : IDisposable
    {
        void Release(object component);

        object Rent();
    }

    /// <summary>
    /// Thread-safe Component Pool
    /// </summary>
    /// <typeparam name="T">Type of Component</typeparam>
    public interface IComponentPool<T> : IObjectPool<T>, IComponentPool where T: class
    {
        void IComponentPool.Release(object component) =>
            Release((T)component);

        object IComponentPool.Rent() =>
            Get();
    }
}
