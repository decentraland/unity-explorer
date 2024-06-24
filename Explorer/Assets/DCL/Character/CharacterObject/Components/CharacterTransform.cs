using DCL.Optimization.Pools;
using System;
using UnityEngine;
using Utility;

namespace DCL.Character.Components
{
    /// <summary>
    ///     Character Transform is not a regular <see cref="TransformComponent" />
    ///     as it's driven by physics and does not have a parent and children
    /// </summary>
    public readonly struct CharacterTransform : IPoolableComponentProvider<Transform>
    {
        public readonly Transform Transform;

        public Vector3 Position => Transform.position;

        public Quaternion Rotation => Transform.rotation;

        public Vector2Int Parcel => ParcelMathHelper.FloorToParcel(Transform.position);

        public CharacterTransform(Transform transform)
        {
            Transform = transform;
        }

        public void Dispose() { }

        Transform IPoolableComponentProvider<Transform>.PoolableComponent => Transform;
        Type IPoolableComponentProvider<Transform>.PoolableComponentType => typeof(Transform);
    }
}
