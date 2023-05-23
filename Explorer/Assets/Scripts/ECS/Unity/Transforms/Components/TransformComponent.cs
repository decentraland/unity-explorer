using ECS.ComponentsPooling;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.Transforms.Components
{
    /// <summary>
    ///     A wrapper over the transform unity component that is created from the SDKTransform component.
    /// </summary>
    public class TransformComponent : IPoolableComponentProvider
    {
        public Transform Transform;
        public HashSet<TransformComponent> Children;
        public TransformComponent Parent;

        public TransformComponent(Transform transform)
        {
            Transform = transform;
            Children = new HashSet<TransformComponent>();
            Parent = null;
        }

        object IPoolableComponentProvider.PoolableComponent => Transform;
        Type IPoolableComponentProvider.PoolableComponentType => typeof(Transform);
    }
}
