using DCL.Character.CharacterCamera.Components;
using DCL.Character.CharacterCamera.Settings;
using UnityEngine;

namespace DCL.Character.CharacterCamera
{
    public static class CameraMovementUtils
    {
        public static void Rotate(ref CameraDampedPOV pov, Transform target, Vector2 lookInput, CameraMovementPOVSettings settings, float deltaTime)
        {
            Vector2 targetRotation = lookInput * settings.RotationSpeed;
            pov.Current = Vector2.SmoothDamp(pov.Current, targetRotation, ref pov.Velocity, settings.RotationDamping);

            float horizontalRotation = Mathf.Clamp(pov.Current.x * deltaTime, -settings.MaxRotationPerFrame, settings.MaxRotationPerFrame);
            float verticalRotation = Mathf.Clamp(pov.Current.y * deltaTime, -settings.MaxRotationPerFrame, settings.MaxRotationPerFrame);

            target.Rotate(Vector3.up, horizontalRotation, Space.World);

            float newVerticalAngle = target.eulerAngles.x - verticalRotation;
            if (newVerticalAngle > 180f) newVerticalAngle -= 360f;
            newVerticalAngle = Mathf.Clamp(newVerticalAngle, settings.MinVerticalAngle, settings.MaxVerticalAngle);

            target.localRotation = Quaternion.Euler(newVerticalAngle, target.eulerAngles.y, target.eulerAngles.z);
        }
    }
}
