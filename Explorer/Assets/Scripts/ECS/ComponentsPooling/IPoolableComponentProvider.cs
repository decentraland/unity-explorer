using System;

namespace ECS.ComponentsPooling
{
    public interface IPoolableComponentProvider : IDisposable
    {
        object PoolableComponent { get; }

        Type PoolableComponentType { get; }

    }
}
