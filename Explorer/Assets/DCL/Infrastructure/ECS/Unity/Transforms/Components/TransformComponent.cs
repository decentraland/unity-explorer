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

            public CachedTransform(Transform transform) : this()
            {
                Update(transform);
            }

            public void Update(Transform transform)
            {
                WorldPosition = transform.position;
                WorldRotation = transform.rotation;
            }
        }

        public CachedTransform Cached;

        public Transform Transform;
        public readonly HashSet<Entity> Children;
        public Entity Parent;

        public TransformComponent(GameObject gameObject) : this(gameObject.transform) { }

        public TransformComponent(Transform transform)
        {
            Transform = transform;
            Children = HashSetPool<Entity>.Get()!;
            Parent = Entity.Null;

            Cached = new CachedTransform(transform);
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
            Cached.Update(Transform);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Apply(Quaternion rotation)
        {
            Cached.WorldRotation = Transform.rotation = rotation;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Apply(Vector3 worldPosition)
        {
            Cached.WorldPosition = Transform.position = worldPosition;
        }

        readonly Transform IPoolableComponentProvider<Transform>.PoolableComponent => Transform;

        Type IPoolableComponentProvider<Transform>.PoolableComponentType => typeof(Transform);

        public void Dispose()
        {
            HashSetPool<Entity>.Release(Children);
        }

        public override readonly string ToString() =>
            $"({nameof(TransformComponent)} {nameof(Parent)}: {Parent}; {nameof(Transform.localPosition)}: {Transform.localPosition}; {nameof(Transform.localRotation)}: {Transform.localRotation}; {nameof(Transform.localScale)} {Transform.localScale})";
    }
}
