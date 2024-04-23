using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.IK
{
    public static class ApplyHeadLookAt
    {
        // This method updates the head IK targets (horizontal and vertical) based on a target look-at direction
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(Vector3 targetDirection, AvatarBase avatarBase, float dt, ICharacterControllerSettings settings)
        {
            Transform reference = avatarBase.HeadPositionConstraint;
            Vector3 referenceAngle = Quaternion.LookRotation(reference.forward).eulerAngles;
            Vector3 targetAngle = Quaternion.LookRotation(targetDirection).eulerAngles;

            float horizontalAngle = Mathf.DeltaAngle(referenceAngle.y, targetAngle.y);

            Quaternion horizontalTargetRotation;
            Quaternion verticalTargetRotation;

            float rotationSpeed = settings.HeadIKRotationSpeed;

            //If the target horizonal angle is outside of the limits, reset the head location to look frontal
            if (Mathf.Abs(horizontalAngle) > settings.HeadIKHorizontalAngleReset)
            {
                //set horizontal rotation to 0
                horizontalTargetRotation = Quaternion.AngleAxis(0, Vector3.up);

                //set vertical rotation to 0
                verticalTargetRotation = horizontalTargetRotation * Quaternion.AngleAxis(0, Vector3.right);
                rotationSpeed = rotationSpeed / 3;
            }
            //otherwise, calculate rotation within constraints
            else
            {

                //clamp horizontal angle and apply rotation
                horizontalAngle = Mathf.Clamp(horizontalAngle, -settings.HeadIKHorizontalAngleLimit, settings.HeadIKHorizontalAngleLimit);
                horizontalTargetRotation = Quaternion.AngleAxis(horizontalAngle, Vector3.up);

                // To avoid breaking the avatar's spine when going from left to right, we slowly move towards the correct angle instead of directly rotating towards the target rotation
                /*
                float currentHorizontalAngle = Mathf.DeltaAngle(referenceAngle.y, avatarBase.HeadLookAtTargetHorizontal.eulerAngles.y);
                if (horizontalAngle >= 0 && currentHorizontalAngle < 0)
                    horizontalTargetRotation = Quaternion.AngleAxis(dt * rotationSpeed, Vector3.up);
                else if (horizontalAngle < 0 && currentHorizontalAngle >= 0)
                    horizontalTargetRotation = Quaternion.AngleAxis(-(dt * rotationSpeed), Vector3.up);
                */

                //calculate vertical angle difference between reference and target, clamped to maximum angle
                float verticalAngle = Mathf.DeltaAngle(referenceAngle.x, targetAngle.x);
                verticalAngle = Mathf.Clamp(verticalAngle, -settings.HeadIKVerticalAngleLimit, settings.HeadIKVerticalAngleLimit);

                //calculate vertical rotation
                verticalTargetRotation = horizontalTargetRotation * Quaternion.AngleAxis(verticalAngle, Vector3.right);
            }

            //apply horizontal rotation
            Quaternion newHorizontalRotation = Quaternion.RotateTowards(avatarBase.HeadLookAtTargetHorizontal.localRotation, horizontalTargetRotation, dt * rotationSpeed);
            avatarBase.HeadLookAtTargetHorizontal.localRotation = newHorizontalRotation;

            //apply vertical rotation
            Quaternion newVerticalRotation = Quaternion.RotateTowards(avatarBase.HeadLookAtTargetVertical.localRotation, verticalTargetRotation, dt * rotationSpeed);
            avatarBase.HeadLookAtTargetVertical.localRotation = newVerticalRotation;
        }
    }
}
