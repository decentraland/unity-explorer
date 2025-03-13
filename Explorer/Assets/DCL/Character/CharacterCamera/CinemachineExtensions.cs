using DCL.CharacterCamera.Components;
using UnityEngine;

namespace DCL.CharacterCamera
{
    public static class CinemachineExtensions
    {
        public static void ForceThirdPersonCameraLookAt(this ICinemachinePreset cinemachinePreset, CameraLookAtIntent lookAtIntent)
        {
            (float horizontalAxis, float verticalAxis) = GetHorizontalAndVerticalAxisForIntent(lookAtIntent);
            cinemachinePreset.ThirdPersonCameraData.Camera.m_XAxis.Value = horizontalAxis;
            cinemachinePreset.ThirdPersonCameraData.Camera.m_YAxis.Value = verticalAxis;
        }

        public static void ForceFirstPersonCameraLookAt(this ICinemachinePreset cinemachinePreset, CameraLookAtIntent lookAtIntent)
        {
            if (cinemachinePreset.FirstPersonCameraData.POV == null) return;

            (float horizontalAxis, float verticalAxis) = GetHorizontalAndVerticalAxisForIntent(lookAtIntent);
            cinemachinePreset.FirstPersonCameraData.POV.m_HorizontalAxis.Value = horizontalAxis;
            cinemachinePreset.FirstPersonCameraData.POV.m_VerticalAxis.Value = verticalAxis;
        }

        public static void ForceDroneCameraLookAt(this ICinemachinePreset cinemachinePreset, CameraLookAtIntent lookAtIntent)
        {
            (float horizontalAxis, float verticalAxis) = GetHorizontalAndVerticalAxisForIntent(lookAtIntent);
            cinemachinePreset.DroneViewCameraData.Camera.m_XAxis.Value = horizontalAxis;
            cinemachinePreset.DroneViewCameraData.Camera.m_YAxis.Value = verticalAxis;
        }

        private static (float, float) GetHorizontalAndVerticalAxisForIntent(CameraLookAtIntent lookAtIntent)
        {
            var eulerDir = Vector3.zero;
            var cameraTarget = lookAtIntent.LookAtTarget;
            float horizontalAxisLookAt = lookAtIntent.PlayerPosition.y - cameraTarget.y;
            var verticalAxisLookAt = new Vector3(cameraTarget.x - lookAtIntent.PlayerPosition.x, 0, cameraTarget.z - lookAtIntent.PlayerPosition.z);

            if (verticalAxisLookAt is { x: 0, y: 0, z: 0 })
                verticalAxisLookAt = Vector3.forward;

            eulerDir.y = Vector3.SignedAngle(Vector3.forward, verticalAxisLookAt, Vector3.up);
            eulerDir.x = Mathf.Atan2(horizontalAxisLookAt, verticalAxisLookAt.magnitude) * Mathf.Rad2Deg;

            //value range 0 to 1, being 0 the bottom orbit and 1 the top orbit
            float yValue = Mathf.InverseLerp(-90, 90, eulerDir.x);

            return (eulerDir.y, yValue);
        }
    }
}
