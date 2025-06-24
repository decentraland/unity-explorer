using System;

namespace DCL.Optimization.Pools
{
    public interface IPoolableComponentProvider<out T> : IDisposable where T: class
    {
        bool IsDisposed { get; }

        T PoolableComponent { get; }

        Type PoolableComponentType { get; }
    }
}
