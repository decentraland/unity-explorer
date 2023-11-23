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
        internal static void Execute(Vector3 targetDirection, AvatarBase avatarBase, float dt, ICharacterControllerSettings settings)
        {
            Transform reference = avatarBase.HeadPositionConstraint;

            Vector3 referenceAngle = Quaternion.LookRotation(reference.forward).eulerAngles;
            Vector3 targetAngle = Quaternion.LookRotation(targetDirection).eulerAngles;

            float horizontalAngle = Mathf.DeltaAngle(referenceAngle.y, targetAngle.y);
            float currentHorizontalAngle = Mathf.DeltaAngle(referenceAngle.y, avatarBase.HeadLookAtTargetHorizontal.eulerAngles.y);

            horizontalAngle = Mathf.Clamp(horizontalAngle, -settings.HeadIKHorizontalAngleLimit,
                settings.HeadIKHorizontalAngleLimit);

            // No Vertical rotation, see below
            var horizontalTargetRotation = Quaternion.AngleAxis(horizontalAngle, Vector3.up);

            // To avoid breaking the avatar's spine when going from left to right, we slowly move towards the correct angle instead of directly rotating towards the target rotation
            if (horizontalAngle >= 0 && currentHorizontalAngle < 0)
                horizontalTargetRotation = Quaternion.AngleAxis(dt * settings.HeadIKRotationSpeed, Vector3.up);
            else if (horizontalAngle < 0 && currentHorizontalAngle >= 0)
                horizontalTargetRotation = Quaternion.AngleAxis(-(dt * settings.HeadIKRotationSpeed), Vector3.up);

            Quaternion newHorizontalRotation = avatarBase.HeadLookAtTargetHorizontal.localRotation;
            newHorizontalRotation = Quaternion.RotateTowards(newHorizontalRotation, horizontalTargetRotation, dt * settings.HeadIKRotationSpeed);

            avatarBase.HeadLookAtTargetHorizontal.localRotation = newHorizontalRotation;

            UpdateVerticalRotation(avatarBase, dt, settings, referenceAngle, targetAngle, horizontalTargetRotation);
        }



        // In order to avoid moving the character hands backwards when bending the spine while looking up/down we implemented a second IK pass
        // This second pass contains the current horizontal target rotation and also applies the vertical rotation
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void UpdateVerticalRotation(AvatarBase avatarBase, float dt, ICharacterControllerSettings settings,
            Vector3 referenceAngle, Vector3 targetAngle, Quaternion horizontalTargetRotation)
        {
            Quaternion currentVerticalRotation = avatarBase.HeadLookAtTargetVertical.localRotation;
            float verticalAngle = Mathf.DeltaAngle(referenceAngle.x, targetAngle.x);
            verticalAngle = Mathf.Clamp(verticalAngle, -settings.HeadIKVerticalAngleLimit, settings.HeadIKVerticalAngleLimit);
            Quaternion verticalRotation = horizontalTargetRotation * Quaternion.AngleAxis(verticalAngle, Vector3.right);
            currentVerticalRotation = Quaternion.RotateTowards(currentVerticalRotation, verticalRotation, dt * settings.HeadIKRotationSpeed);
            avatarBase.HeadLookAtTargetVertical.localRotation = currentVerticalRotation;
        }


    }
}
