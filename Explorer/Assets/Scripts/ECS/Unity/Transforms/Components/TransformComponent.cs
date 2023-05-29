using System.Collections.Generic;
using Arch.Core;
using ECS.ComponentsPooling;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.Unity.Transforms.Components
{
    /// <summary>
    ///     A wrapper over the transform unity component that is created from the SDKTransform component.
    ///     It's the Unity base representation of an Entity.
    /// </summary>
    public struct TransformComponent : IPoolableComponentProvider<Transform>
    {
        public Transform Transform;
        public HashSet<EntityReference> Children;
        public EntityReference Parent;

        public TransformComponent(Transform transform)
        {
            Transform = transform;
            Children = HashSetPool<EntityReference>.Get();
            Parent = EntityReference.Null;
        }

        Transform IPoolableComponentProvider<Transform>.PoolableComponent => Transform;

        public void Dispose()
        {
            HashSetPool<EntityReference>.Release(Children);
        }
    }
}
