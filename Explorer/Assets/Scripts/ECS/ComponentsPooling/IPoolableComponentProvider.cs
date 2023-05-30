using System;

namespace ECS.ComponentsPooling
{
    public interface IPoolableComponentProvider<out T> : IDisposable where T: class
    {
        T PoolableComponent { get; }
        Type PoolableComponentType => typeof(T);
    }
}
