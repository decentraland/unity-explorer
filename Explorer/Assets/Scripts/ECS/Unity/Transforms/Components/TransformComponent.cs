using Arch.Core;
using DCL.Optimization.Pools;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        public struct CachedTransform
        {
            public Vector3 WorldPosition;
            public Quaternion WorldRotation;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
            public Vector3 LocalScale;
        }

        public CachedTransform Cached;

        public Transform Transform;
        public readonly HashSet<EntityReference> Children;
        public EntityReference Parent;

        public TransformComponent(GameObject gameObject) : this(gameObject.transform) { }

        public TransformComponent(Transform transform)
        {
            Transform = transform;
            Children = HashSetPool<EntityReference>.Get();
            Parent = EntityReference.Null;

            Cached = new CachedTransform
            {
                WorldPosition = transform.position,
                WorldRotation = transform.rotation,
                LocalPosition = transform.localPosition,
                LocalRotation = transform.localRotation,
                LocalScale = transform.localScale,
            };
        }

        public TransformComponent(Transform transform, string name, Vector3 startPosition) : this(transform)
        {
            transform.name = name;
            transform.localPosition = startPosition;
        }

        public void SetTransform(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        {
            Transform.SetLocalPositionAndRotation(localPosition, localRotation);
            Transform.localScale = localScale;

            UpdateCache();
        }

        public void SetWorldTransform(Vector3 worldPosition, Quaternion worldRotation, Vector3 localScale)
        {
            Transform.SetPositionAndRotation(worldPosition, worldRotation);
            Transform.localScale = localScale;

            UpdateCache();
        }

        public void SetTransform(Transform transform)
        {
            SetTransform(transform.localPosition, transform.localRotation, transform.localScale);
        }

        public void UpdateCache()
        {
            Cached.LocalPosition = Transform.localPosition;
            Cached.LocalRotation = Transform.localRotation;
            Cached.LocalScale = Transform.localScale;
            Cached.WorldPosition = Transform.position;
            Cached.WorldRotation = Transform.rotation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Apply(Quaternion rotation)
        {
            Cached.WorldRotation = Transform.rotation = rotation;
            Cached.LocalRotation = Transform.localRotation;
        }

        Transform IPoolableComponentProvider<Transform>.PoolableComponent => Transform;

        Type IPoolableComponentProvider<Transform>.PoolableComponentType => typeof(Transform);

        public void Dispose()
        {
            HashSetPool<EntityReference>.Release(Children);
        }
    }
}
