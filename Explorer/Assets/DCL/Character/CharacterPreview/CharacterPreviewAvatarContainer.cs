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

        [field: SerializeField] internal Vector3 previewPositionInScene { get; private set; }
        [field: SerializeField] internal Transform avatarParent { get; private set; }
        [field: SerializeField] internal Camera camera { get; private set; }
        [field: SerializeField] internal Transform cameraTarget { get; private set; }
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

            // FOV
            TargetFOV = freeLookCamera.m_Lens.FieldOfView;
            fovTransitionStartTime = Time.time;
            fovTransitionStartValue = freeLookCamera.m_Lens.FieldOfView;
            isFOVTransitioning = false;

            camera.gameObject.TryGetComponent(out UniversalAdditionalCameraData cameraData);

            //We disable post processing on all platforms as the shader is not working correctly and it shows a black background
            cameraData.renderPostProcessing = false;
        }

        public void DeInitialize()
        {
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
            fovTransitionStartTime = Time.time;
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
            if (IsDragging && Time.time - LastDragTime > DRAG_TIMEOUT)
                IsDragging = false;

            // If not dragging, decelerate
            if (!IsDragging)
            {
                if (RotationInertia <= 0f)
                {
                    AngularVelocity = 0f;
                    return;
                }

                // Deceleration, higher inertia = faster deceleration
                float decelerationRate = RotationInertia * ANGULAR_VELOCITY_DECELERATION_COEFF * Time.deltaTime;
                float velocitySign = Mathf.Sign(AngularVelocity);
                float velocityMagnitude = Mathf.Abs(AngularVelocity);

                velocityMagnitude -= decelerationRate;

                if (velocityMagnitude <= 0f)
                    AngularVelocity = 0f;
                else
                    AngularVelocity = velocitySign * velocityMagnitude;
            }

            // Apply rotation if there's any angular velocity
            if (Mathf.Abs(AngularVelocity) > ANGULAR_VELOCITY_LOWER_THRES)
            {
                Vector3 rotation = rotationTarget.rotation.eulerAngles;

                float rotationAmount = AngularVelocity * RotationModifier * Time.deltaTime;

                rotation.y += rotationAmount;
                rotationTarget.rotation = Quaternion.Euler(rotation);
            }
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
                float elapsedTime = Time.time - fovTransitionStartTime;
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
                float t = 1f - Mathf.Exp(-FOV_SPEED_COEFF * Time.deltaTime);
                newFOV = Mathf.Lerp(currentFOV, TargetFOV, t);
            }

            freeLookCamera.m_Lens.FieldOfView = newFOV;
        }

        public void ResetAvatarMovement()
        {
            // Reset rotation
            rotationTarget.rotation = Quaternion.identity;
            AngularVelocity = 0f;
            IsDragging = false;
            LastDragTime = 0f;

            // Reset FOV
            TargetFOV = freeLookCamera.m_Lens.FieldOfView;
            fovTransitionStartTime = Time.time;
            fovTransitionStartValue = freeLookCamera.m_Lens.FieldOfView;
            isFOVTransitioning = false;
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
