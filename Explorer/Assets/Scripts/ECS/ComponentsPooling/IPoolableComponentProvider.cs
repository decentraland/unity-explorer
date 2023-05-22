using System;

namespace ECS.ComponentsPooling
{
    public interface IPoolableComponentProvider
    {
        object PoolableComponent { get; }

        Type PoolableComponentType { get; }
    }
}
