using Arch.Core;
using ECS.ComponentsPooling;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.Unity.Transforms.Components
{
    /// <summary>
    ///     A wrapper over the transform unity component that is created from the SDKTransform component.
    ///     It's the Unity base representation of an Entity.
    /// </summary>
    public struct TransformComponent : IPoolableComponentProvider
    {
        public Transform Transform;
        public HashSet<TransformComponent> Children;
        public EntityReference Parent;

        public TransformComponent(Transform transform)
        {
            Transform = transform;
            Children = new HashSet<TransformComponent>();
            Parent = EntityReference.Null;
        }

        object IPoolableComponentProvider.PoolableComponent => Transform;
        Type IPoolableComponentProvider.PoolableComponentType => typeof(Transform);
    }
}
