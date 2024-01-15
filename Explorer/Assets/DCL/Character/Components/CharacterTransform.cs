using DCL.Optimization.Pools;
using System;
using UnityEngine;

namespace DCL.Character.Components
{
    /// <summary>
    ///     Character Transform is not a regular <see cref="TransformComponent" />
    ///     as it's driven by physics and does not have a parent and children
    /// </summary>
    public readonly struct CharacterTransform : IPoolableComponentProvider<Transform>
    {
        private readonly Transform transform;

        public CharacterTransform(Transform transform)
        {
            this.transform = transform;
        }

        public Vector3 Position => transform.position;

        public Quaternion Rotation => transform.rotation;

        public void Dispose() { }

        Transform IPoolableComponentProvider<Transform>.PoolableComponent => transform;
        Type IPoolableComponentProvider<Transform>.PoolableComponentType => typeof(Transform);
    }
}
