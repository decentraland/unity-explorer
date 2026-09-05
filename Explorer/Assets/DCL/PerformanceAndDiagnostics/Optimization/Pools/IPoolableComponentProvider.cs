using System;

namespace DCL.Optimization.Pools
{
    public interface IPoolableComponentProvider<out T> : IDisposable where T: class
    {
        T PoolableComponent { get; }

        Type PoolableComponentType { get; }
    }
}
