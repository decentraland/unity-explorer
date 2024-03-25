using Cinemachine;
using DCL.CharacterCamera.Components;
using UnityEngine;

namespace DCL.CharacterCamera
{
    public static class CinemachineExtensions
    {
        public static void ForceThirdPersonCameraLookAt(this ICinemachinePreset cinemachinePreset, CameraLookAtIntent lookAtIntent)
        {
            CinemachineFreeLook tpc = cinemachinePreset.ThirdPersonCameraData.Camera;

            var eulerDir = Vector3.zero;
            var cameraTarget = lookAtIntent.LookAtTarget;
            float horizontalAxisLookAt = lookAtIntent.PlayerPosition.y - cameraTarget.y;
            var verticalAxisLookAt = new Vector3(cameraTarget.x - lookAtIntent.PlayerPosition.x, 0, cameraTarget.z - lookAtIntent.PlayerPosition.z);

            if (verticalAxisLookAt is { x: 0, y: 0, z: 0 })
                verticalAxisLookAt = Vector3.forward;

            eulerDir.y = Vector3.SignedAngle(Vector3.forward, verticalAxisLookAt, Vector3.up);
            eulerDir.x = Mathf.Atan2(horizontalAxisLookAt, verticalAxisLookAt.magnitude) * Mathf.Rad2Deg;

            tpc.m_XAxis.Value = eulerDir.y;

            //value range 0 to 1, being 0 the bottom orbit and 1 the top orbit
            float yValue = Mathf.InverseLerp(-90, 90, eulerDir.x);
            tpc.m_YAxis.Value = yValue;
        }

        public static void ForceFirstPersonCameraLookAt(this ICinemachinePreset cinemachinePreset, CameraLookAtIntent lookAtIntent)
        {
            var eulerDir = Vector3.zero;
            var cameraTarget = lookAtIntent.LookAtTarget;
            float horizontalAxisLookAt = lookAtIntent.PlayerPosition.y - cameraTarget.y;
            var verticalAxisLookAt = new Vector3(cameraTarget.x - lookAtIntent.PlayerPosition.x, 0, cameraTarget.z - lookAtIntent.PlayerPosition.z);

            eulerDir.y = Vector3.SignedAngle(Vector3.forward, verticalAxisLookAt, Vector3.up);
            eulerDir.x = Mathf.Atan2(horizontalAxisLookAt, verticalAxisLookAt.magnitude) * Mathf.Rad2Deg;

            if (cinemachinePreset.FirstPersonCameraData.POV != null)
            {
                cinemachinePreset.FirstPersonCameraData.POV.m_HorizontalAxis.Value = eulerDir.y;
                cinemachinePreset.FirstPersonCameraData.POV.m_VerticalAxis.Value = eulerDir.x;
            }
        }

        public static void ForceFreeCameraLookAt(this ICinemachinePreset cinemachinePreset, CameraLookAtIntent lookAtIntent)
        {
            var newPos = new Vector3(lookAtIntent.PlayerPosition.x, lookAtIntent.PlayerPosition.y, lookAtIntent.PlayerPosition.z);
            var cameraTarget = lookAtIntent.LookAtTarget;
            var dirToLook = cameraTarget - newPos;
            var eulerDir = Quaternion.LookRotation(dirToLook).eulerAngles;

            if (cinemachinePreset.FreeCameraData.POV != null)
            {
                cinemachinePreset.FreeCameraData.POV.m_HorizontalAxis.Value = eulerDir.y;
                cinemachinePreset.FreeCameraData.POV.m_VerticalAxis.Value = eulerDir.x;
            }
        }
    }
}
