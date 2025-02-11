using DCL.Utilities.Extensions;
using UnityEngine;

namespace DCL.Character.Components
{
    public struct CharacterTargetPosition
    {
        public Vector3 LastPosition;
        public Vector3 TargetPosition;
        public Quaternion FinalRotation;

        public float DistanceToTarget => Vector3.Distance(TargetPosition, LastPosition);
        public Vector3 DirectionVector => LastPosition.GetDirection(TargetPosition);
        public Vector3 FlattenDirectionVector => LastPosition.GetYFlattenDirection(TargetPosition);

        public CharacterTargetPosition(Vector3 lastPosition, Vector3 targetPosition, Quaternion finalRotation)
        {
            LastPosition = lastPosition;
            TargetPosition = targetPosition;
            FinalRotation = finalRotation;
        }
    }
}
