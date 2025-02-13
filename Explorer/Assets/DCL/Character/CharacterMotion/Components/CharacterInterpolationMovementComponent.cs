using UnityEngine;

namespace DCL.Character.CharacterMotion.Components
{
    public struct CharacterInterpolationMovementComponent
    {
        public Vector3 LastPosition;
        public Vector3 TargetPosition;
        public Quaternion TargetRotation;
        public bool IsPositionManagedByTween;
        public bool IsRotationManagedByTween;

        public CharacterInterpolationMovementComponent(
            Vector3 lastPosition,
            Vector3 targetPosition,
            Quaternion targetRotation,
            bool isPositionManagedByTween = false,
            bool isRotationManagedByTween = false)
        {
            LastPosition = lastPosition;
            TargetPosition = targetPosition;
            TargetRotation = targetRotation;
            IsPositionManagedByTween = isPositionManagedByTween;
            IsRotationManagedByTween = isRotationManagedByTween;
        }
    }
}
