using Cinemachine;
using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DCL.CharacterPreview
{
    /// <summary>
    ///     Contains serialized data only needed for the character preview
    ///     See CharacterPreviewController in the old renderer
    /// </summary>
    public class CharacterPreviewAvatarContainer : MonoBehaviour, IDisposable
    {
        private const float DRAG_TIMEOUT = 0.1f;
        private const float ANGULAR_VELOCITY_DECELERATION_COEFF = 900f;
        private const float ANGULAR_VELOCITY_LOWER_THRES = 0.01f;
        private const float FOW_LOWER_THRES = 0.01f;
        private const float FOV_SPEED_COEFF = 3.5f;
        private const float FOV_SMOOTH_TIME = 0.6f;

        private float fovTransitionStartTime;
        private float fovTransitionStartValue;
        private bool isFOVTransitioning;
        private float cameraPitch;

        [field: SerializeField] internal Vector3 previewPositionInScene { get; private set; }
        [field: SerializeField] internal Transform avatarParent { get; private set; }
        [field: SerializeField] internal Camera camera { get; private set; }
        [field: SerializeField] internal Transform cameraTarget { get; private set; }
        [field: SerializeField] internal Transform cameraPivot { get; private set; }

        /// <summary>
        ///     Height, in the container's local space, that the orbiting camera may not descend below.
        /// </summary>
        [field: SerializeField] internal float cameraFloorHeight { get; private set; }
        [field: SerializeField] internal Transform rotationTarget { get; private set; }
        [field: SerializeField] internal CinemachineFreeLook freeLookCamera { get; private set; }
        [field: SerializeField] internal GameObject previewPlatform { get; private set; }
        [field: SerializeField] internal AvatarPreviewHeadIKSettings headIKSettings { get; private set; }

        internal float TargetFOV { get; set; }
        internal float RotationModifier { get; set; }
        internal float RotationInertia { get; set; }
        internal bool IsDragging { get; set; }
        internal float LastDragTime { get; set; }
        internal float AngularVelocity { get; set; }
        internal float VerticalRotationModifier { get; set; }
        internal float MaxVerticalAngle { get; set; }
        internal float VerticalAngularVelocity { get; set; }

        public void Dispose()
        {
        }

        public void Initialize(RenderTexture targetTexture, Vector3 position)
        {
            transform.position = position;
            camera.targetTexture = targetTexture;

            // Rotation
            rotationTarget.rotation = Quaternion.identity;
            AngularVelocity = 0f;
            IsDragging = false;
            LastDragTime = 0f;
            ResetVerticalRotation();

            // FOV
            TargetFOV = freeLookCamera.m_Lens.FieldOfView;
            fovTransitionStartTime = UnityEngine.Time.time;
            fovTransitionStartValue = freeLookCamera.m_Lens.FieldOfView;
            isFOVTransitioning = false;

            camera.gameObject.TryGetComponent(out UniversalAdditionalCameraData cameraData);

            //We disable post processing on all platforms as the shader is not working correctly and it shows a black background
            cameraData.renderPostProcessing = false;
        }

        public void DeInitialize()
        {
            if(camera != null)
                camera.targetTexture = null!;
        }

        public void SetCameraPosition(CharacterPreviewCameraPreset preset)
        {
            if (cameraTarget != null)
                cameraTarget.localPosition = preset.verticalPosition;

            StartFOVTransition(preset.cameraFieldOfView);
        }

        public void StartFOVTransition(float targetFOV)
        {
            TargetFOV = targetFOV;
            fovTransitionStartTime = UnityEngine.Time.time;
            fovTransitionStartValue = freeLookCamera.m_Lens.FieldOfView;
            isFOVTransitioning = true;
        }

        public void SetPreviewPlatformActive(bool isActive) =>
            previewPlatform.SetActive(isActive);

        private void Update()
        {
            UpdateRotation();
            UpdateFOV();
        }

        private void UpdateRotation()
        {
            // Check if dragging has timed out
            if (IsDragging && UnityEngine.Time.time - LastDragTime > DRAG_TIMEOUT)
                IsDragging = false;

            // If not dragging, decelerate
            if (!IsDragging)
            {
                if (RotationInertia <= 0f)
                {
                    AngularVelocity = 0f;
                    VerticalAngularVelocity = 0f;
                    return;
                }

                // Deceleration, higher inertia = faster deceleration
                float decelerationRate = RotationInertia * ANGULAR_VELOCITY_DECELERATION_COEFF * UnityEngine.Time.deltaTime;

                AngularVelocity = Decelerate(AngularVelocity, decelerationRate);
                VerticalAngularVelocity = Decelerate(VerticalAngularVelocity, decelerationRate);
            }

            // Apply rotation if there's any angular velocity
            if (Mathf.Abs(AngularVelocity) > ANGULAR_VELOCITY_LOWER_THRES)
            {
                Vector3 rotation = rotationTarget.rotation.eulerAngles;

                float rotationAmount = AngularVelocity * RotationModifier * UnityEngine.Time.deltaTime;

                rotation.y += rotationAmount;
                rotationTarget.rotation = Quaternion.Euler(rotation);
            }

            if (Mathf.Abs(VerticalAngularVelocity) > ANGULAR_VELOCITY_LOWER_THRES)
                UpdateCameraPitch();
        }

        private static float Decelerate(float angularVelocity, float decelerationRate)
        {
            float velocityMagnitude = Mathf.Abs(angularVelocity) - decelerationRate;

            return velocityMagnitude <= 0f ? 0f : Mathf.Sign(angularVelocity) * velocityMagnitude;
        }

        private void UpdateCameraPitch()
        {
            float tiltedPitch = cameraPitch + (VerticalAngularVelocity * VerticalRotationModifier * UnityEngine.Time.deltaTime);
            cameraPitch = Mathf.Clamp(tiltedPitch, -MaxVerticalAngle, Mathf.Min(MaxVerticalAngle, FloorPitchLimit()));

            // Drop the inertia at the limit, or a flick keeps "arriving" after the camera has visibly stopped.
            if (!Mathf.Approximately(cameraPitch, tiltedPitch))
                VerticalAngularVelocity = 0f;

            ApplyCameraPitch();
        }

        /// <summary>
        ///     Downward pitch that sets the camera down on <see cref="cameraFloorHeight"/>. Descending past it
        ///     would shoot the avatar from under its platform.
        /// </summary>
        private float FloorPitchLimit()
        {
            // Undoing the pivot's rotation gives the camera's offset at rest, whatever the preset framed and
            // wherever the pan left it. The rig turns rigidly about the pivot, so from there the camera's
            // height traces radius * cos(pitch + phase); the floor is where that lands.
            Vector3 restingOffset = cameraPivot.InverseTransformPoint(camera.transform.position);
            float radius = new Vector2(restingOffset.y, restingOffset.z).magnitude;
            float floorAbovePivot = cameraFloorHeight - cameraPivot.localPosition.y;
            float phase = Mathf.Atan2(restingOffset.z, restingOffset.y) * Mathf.Rad2Deg;

            return (Mathf.Acos(Mathf.Clamp(floorAbovePivot / radius, -1f, 1f)) * Mathf.Rad2Deg) - phase;
        }

        private void UpdateFOV()
        {
            float currentFOV = freeLookCamera.m_Lens.FieldOfView;

            // Early return if already at target
            if (Mathf.Abs(currentFOV - TargetFOV) <= FOW_LOWER_THRES)
            {
                isFOVTransitioning = false;
                return;
            }

            float newFOV;

            if (isFOVTransitioning)
            {
                // Smooth transition for category changes
                float elapsedTime = UnityEngine.Time.time - fovTransitionStartTime;
                float normalizedTime = Mathf.Clamp01(elapsedTime / FOV_SMOOTH_TIME);

                float easedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);
                newFOV = Mathf.Lerp(fovTransitionStartValue, TargetFOV, easedTime);

                if (normalizedTime >= 1f)
                {
                    isFOVTransitioning = false;
                    newFOV = TargetFOV;
                }
            }
            else
            {
                // Smooth interpolation for scroll input
                float t = 1f - Mathf.Exp(-FOV_SPEED_COEFF * UnityEngine.Time.deltaTime);
                newFOV = Mathf.Lerp(currentFOV, TargetFOV, t);
            }

            freeLookCamera.m_Lens.FieldOfView = newFOV;
        }

        /// <summary>
        ///     Levels the camera back to its default elevation, keeping the avatar's own rotation and the zoom.
        /// </summary>
        public void ResetVerticalRotation()
        {
            VerticalAngularVelocity = 0f;
            cameraPitch = 0f;
            ApplyCameraPitch();
        }

        public void ResetAvatarMovement()
        {
            // Reset rotation
            rotationTarget.rotation = Quaternion.identity;
            AngularVelocity = 0f;
            IsDragging = false;
            LastDragTime = 0f;
            ResetVerticalRotation();

            // Reset FOV
            TargetFOV = freeLookCamera.m_Lens.FieldOfView;
            fovTransitionStartTime = UnityEngine.Time.time;
            fovTransitionStartValue = freeLookCamera.m_Lens.FieldOfView;
            isFOVTransitioning = false;
        }

        /// <summary>
        ///     Swings the camera over and under the avatar, which stays upright on its platform.
        ///     The rig reads its target's rotation (LockToTargetNoRoll binding) and the target hangs off this
        ///     pivot, so camera and framing point rotate together: the elevation changes and the shot the
        ///     screen was tuned for survives. Pitching the target instead would orbit around the framing
        ///     point, which sits off the avatar's axis wherever the shot is angled.
        /// </summary>
        private void ApplyCameraPitch()
        {
            cameraPivot.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);

            // Position and aim damping would leave the camera seconds behind the drag. The orbit is
            // already smoothed by its own inertia, so the rig takes this rotation undamped and lands on
            // the pose exactly. Damping stays in effect on every frame the orbit does not move.
            freeLookCamera.PreviousStateIsValid = false;
        }
    }

    [Serializable]
    public class AvatarPreviewHeadIKSettings
    {
        public float MinAvatarDepth = 500;
        public float MaxAvatarDepth = 1500;

        public float HeadMoveSpeed = 0.5f;
    }
}
