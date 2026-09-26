using Cinemachine;
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

            // POV axes are in degrees, unlike the 0..1 orbit value the FreeLook rigs consume.
            (float yawDegrees, float pitchDegrees) = GetYawPitchDegrees(lookAtIntent.LookAtTarget, lookAtIntent.PlayerPosition);
            cinemachinePreset.FirstPersonCameraData.POV.m_HorizontalAxis.Value = yawDegrees;
            cinemachinePreset.FirstPersonCameraData.POV.m_VerticalAxis.Value = pitchDegrees;
        }

        public static void ForceDroneCameraLookAt(this ICinemachinePreset cinemachinePreset, CameraLookAtIntent lookAtIntent)
        {
            (float horizontalAxis, float verticalAxis) = GetHorizontalAndVerticalAxisForIntent(lookAtIntent);
            cinemachinePreset.DroneViewCameraData.Camera.m_XAxis.Value = horizontalAxis;
            cinemachinePreset.DroneViewCameraData.Camera.m_YAxis.Value = verticalAxis;
        }

        /// <summary>
        ///     Places the free camera at an absolute world position (and optionally sets its field of view).
        ///     The free camera's position is its vcam transform, the same mechanism free-fly input moves it through.
        /// </summary>
        public static void ForceFreeCameraPose(this ICinemachinePreset cinemachinePreset, Vector3 position, float? fov = null)
        {
            cinemachinePreset.FreeCameraData.Camera.transform.position = position;

            if (fov.HasValue)
                cinemachinePreset.FreeCameraData.Camera.m_Lens.FieldOfView = fov.Value;
        }

        public static void ForceFreeCameraLookAt(this ICinemachinePreset cinemachinePreset, CameraLookAtIntent lookAtIntent)
        {
            CinemachinePOV? pov = cinemachinePreset.FreeCameraData.POV;

            if (pov == null)
                return;

            // The free camera is detached from the player, so the aim originates at the camera itself.
            Vector3 origin = cinemachinePreset.FreeCameraData.Camera.transform.position;
            (float yawDegrees, float pitchDegrees) = GetYawPitchDegrees(lookAtIntent.LookAtTarget, origin);

            pov.m_HorizontalAxis.Value = yawDegrees;
            pov.m_VerticalAxis.Value = pitchDegrees;
        }

        private static (float, float) GetHorizontalAndVerticalAxisForIntent(CameraLookAtIntent lookAtIntent)
        {
            (float yawDegrees, float pitchDegrees) = GetYawPitchDegrees(lookAtIntent.LookAtTarget, lookAtIntent.PlayerPosition);

            //value range 0 to 1, being 0 the bottom orbit and 1 the top orbit
            float yValue = Mathf.InverseLerp(-90, 90, pitchDegrees);

            return (yawDegrees, yValue);
        }

        /// <summary>
        ///     Yaw/pitch in degrees (Unity euler convention: positive pitch looks down) that point from origin at target.
        /// </summary>
        private static (float yawDegrees, float pitchDegrees) GetYawPitchDegrees(Vector3 target, Vector3 origin)
        {
            float heightDelta = origin.y - target.y;
            var flatDirection = new Vector3(target.x - origin.x, 0, target.z - origin.z);

            if (flatDirection is { x: 0, y: 0, z: 0 })
                flatDirection = Vector3.forward;

            float yawDegrees = Vector3.SignedAngle(Vector3.forward, flatDirection, Vector3.up);
            float pitchDegrees = Mathf.Atan2(heightDelta, flatDirection.magnitude) * Mathf.Rad2Deg;

            return (yawDegrees, pitchDegrees);
        }
    }
}
