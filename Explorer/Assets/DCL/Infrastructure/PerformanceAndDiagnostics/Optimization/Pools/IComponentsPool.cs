using System;

namespace DCL.Optimization.Pools
{
    public interface IComponentPool : IDisposable
    {
        void Release(object component);

        object Rent();
    }

    /// <summary>
    ///     Thread-safe Component Pool
    /// </summary>
    /// <typeparam name="T">Type of Component</typeparam>
    public interface IComponentPool<T> : IExtendedObjectPool<T>, IComponentPool where T: class
    {
        void IComponentPool.Release(object component) =>
            Release((T)component);

        object IComponentPool.Rent() =>
            Get();
    }
}
