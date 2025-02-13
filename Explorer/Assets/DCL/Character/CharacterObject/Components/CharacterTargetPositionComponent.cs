using UnityEngine;

namespace DCL.Character.Components
{
    public struct CharacterTargetPositionComponent
    {
        public Vector3 LastPosition;
        public Vector3 TargetPosition;
        public Quaternion FinalRotation;
        public bool IsPositionManagedByTween;
        public bool IsRotationManagedByTween;

        public CharacterTargetPositionComponent(
            Vector3 lastPosition,
            Vector3 targetPosition,
            Quaternion finalRotation,
            bool isPositionManagedByTween = false,
            bool isRotationManagedByTween = false)
        {
            LastPosition = lastPosition;
            TargetPosition = targetPosition;
            FinalRotation = finalRotation;
            IsPositionManagedByTween = isPositionManagedByTween;
            IsRotationManagedByTween = isRotationManagedByTween;
        }
    }
}
