using UnityEngine;

namespace DCL.Character.Components
{
    public struct CharacterTargetPositionComponent
    {
        public Vector3 LastPosition;
        public Vector3 TargetPosition;
        public Quaternion FinalRotation;
        public bool IsManagedByTween;

        public CharacterTargetPositionComponent(Vector3 lastPosition, Vector3 targetPosition, Quaternion finalRotation, bool isManagedByTween = false)
        {
            LastPosition = lastPosition;
            TargetPosition = targetPosition;
            FinalRotation = finalRotation;
            IsManagedByTween = isManagedByTween;
        }
    }
}
