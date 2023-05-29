using System;

namespace ECS.ComponentsPooling
{
    public interface IPoolableComponentProvider<T> : IDisposable where T : class
    {
        T PoolableComponent { get; }
        Type PoolableComponentType => typeof(T);

        void IDisposable.Dispose()
        {
        }
    }
}
