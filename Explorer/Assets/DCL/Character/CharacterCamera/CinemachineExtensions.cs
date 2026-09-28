using Cinemachine;
using DCL.CharacterCamera.Components;
using System;
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

        /// <summary>
        ///     Points the free camera straight down from <paramref name="position" /> with an orthographic projection whose
        ///     half-height is <paramref name="orthographicSize" />. Dispose the result to restore the previous projection.
        /// </summary>
        public static FreeCameraProjectionOverride ForceFreeCameraTopDownOrthographic(this ICinemachinePreset cinemachinePreset, Vector3 position, float orthographicSize) =>
            new (cinemachinePreset, position, orthographicSize);

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

    /// <summary>
    ///     A top-down orthographic override of the free camera. Its state stays private so callers in assemblies
    ///     that do not reference Cinemachine can hold and dispose it.
    /// </summary>
    public sealed class FreeCameraProjectionOverride : IDisposable
    {
        private const float TOP_DOWN_PITCH = 90f;

        private readonly ICinemachinePreset cinemachinePreset;
        private readonly LensSettings.OverrideModes previousModeOverride;
        private readonly float previousOrthographicSize;
        private readonly float previousPitchMin;
        private readonly float previousPitchMax;
        private readonly bool previousOutputCameraOrthographic;

        internal FreeCameraProjectionOverride(ICinemachinePreset cinemachinePreset, Vector3 position, float orthographicSize)
        {
            this.cinemachinePreset = cinemachinePreset;

            CinemachineVirtualCamera vcam = cinemachinePreset.FreeCameraData.Camera;
            CinemachinePOV? pov = cinemachinePreset.FreeCameraData.POV;
            Camera? outputCamera = cinemachinePreset.Brain.OutputCamera;

            previousModeOverride = vcam.m_Lens.ModeOverride;
            previousOrthographicSize = vcam.m_Lens.OrthographicSize;
            previousPitchMin = pov != null ? pov.m_VerticalAxis.m_MinValue : 0f;
            previousPitchMax = pov != null ? pov.m_VerticalAxis.m_MaxValue : 0f;
            previousOutputCameraOrthographic = outputCamera != null && outputCamera.orthographic;

            vcam.transform.position = position;
            vcam.m_Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            vcam.m_Lens.OrthographicSize = orthographicSize;

            if (pov == null) return;

            // The rig clamps pitch just short of vertical; widen it for the capture so the view is exactly top-down.
            pov.m_VerticalAxis.m_MinValue = -TOP_DOWN_PITCH;
            pov.m_VerticalAxis.m_MaxValue = TOP_DOWN_PITCH;
            pov.m_VerticalAxis.Value = TOP_DOWN_PITCH;
            pov.m_HorizontalAxis.Value = 0f;
        }

        public void Dispose()
        {
            CinemachineVirtualCamera vcam = cinemachinePreset.FreeCameraData.Camera;
            CinemachinePOV? pov = cinemachinePreset.FreeCameraData.POV;

            vcam.m_Lens.ModeOverride = previousModeOverride;
            vcam.m_Lens.OrthographicSize = previousOrthographicSize;

            if (pov != null)
            {
                pov.m_VerticalAxis.m_MinValue = previousPitchMin;
                pov.m_VerticalAxis.m_MaxValue = previousPitchMax;
                pov.m_VerticalAxis.Value = Mathf.Clamp(pov.m_VerticalAxis.Value, previousPitchMin, previousPitchMax);
            }

            // The brain only pushes the projection while a lens override is active, so the output camera is reset by hand.
            Camera? outputCamera = cinemachinePreset.Brain.OutputCamera;

            if (outputCamera != null)
                outputCamera.orthographic = previousOutputCameraOrthographic;
        }
    }
}
