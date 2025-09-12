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
        private const float MINIMAL_DISTANCE_DIFFERENCE = 0.01f;
        
        public readonly Transform Transform;
        
        public bool IsDirty { get; private set; }
        
        private Vector3 oldPosition;
        
        public CharacterTransform(Transform transform)
        {
            Transform = transform;
            oldPosition = Transform.position;
            IsDirty = false;
        }

        public Vector3 Position => Transform.position;

        public Quaternion Rotation => Transform.rotation;

        public void Dispose() { }

        public void SetPositionAndRotationWithDirtyCheck(Vector3 position, Quaternion rotation)
        {
            Transform.SetPositionAndRotation(position, rotation);
            TrySetDirty(position);
        }

        public void SetPositionWithDirtyCheck(Vector3 position)
        {
            Transform.position = position;
            TrySetDirty(position);
        }

        public void ClearDirty()
        {
            IsDirty = false;
        }
        
        private void TrySetDirty(Vector3 newPosition)
        {
            if (IsDirty) return;
            
            float distance = CheapDistance(oldPosition, newPosition);

            if (distance < MINIMAL_DISTANCE_DIFFERENCE) return;
            
            oldPosition = newPosition;
            IsDirty = true;
        }

        private float CheapDistance(Vector3 positionA, Vector3 positionB)
        {
            Vector3 diff = positionA - positionB;
            return Mathf.Abs(diff.x) + Mathf.Abs(diff.y) + Mathf.Abs(diff.z);
        }

        Transform IPoolableComponentProvider<Transform>.PoolableComponent => Transform;
        Type IPoolableComponentProvider<Transform>.PoolableComponentType => typeof(Transform);
    }
}
