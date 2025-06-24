using DCL.Optimization.Pools;
using System;
using UnityEngine;

namespace DCL.Character.Components
{
    /// <summary>
    ///     Character Transform is not a regular <see cref="TransformComponent" />
    ///     as it's driven by physics and does not have a parent and children
    /// </summary>
    public struct CharacterTransform : IPoolableComponentProvider<Transform>
    {
        public readonly Transform Transform;

        public bool IsDisposed { get; private set; }

        public CharacterTransform(Transform transform)
        {
            Transform = transform;
            IsDisposed = false;
        }

        public readonly Vector3 Position => Transform.position;

        public readonly Quaternion Rotation => Transform.rotation;

        public void Dispose()
        {
            IsDisposed = true;
        }

        readonly Transform IPoolableComponentProvider<Transform>.PoolableComponent => Transform;
        Type IPoolableComponentProvider<Transform>.PoolableComponentType => typeof(Transform);
    }
}
