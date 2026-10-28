using Cinemachine;
using DCL.Diagnostics;
using System;
using UnityEngine;
using UnityEngine.Rendering;
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
        private bool isFovTransitioning;
        private Light? previewLight;
        private bool fogBeforePreviewRender;

        [field: SerializeField] internal Vector3 previewPositionInScene { get; private set; }
        [field: SerializeField] internal Transform avatarParent { get; private set; } = null!;
        [field: SerializeField] internal Camera camera { get; private set; } = null!;
        [field: SerializeField] internal Transform cameraTarget { get; private set; } = null!;
        [field: SerializeField] internal Transform rotationTarget { get; private set; } = null!;
        [field: SerializeField] internal CinemachineFreeLook freeLookCamera { get; private set; } = null!;
        [field: SerializeField] internal GameObject previewPlatform { get; private set; } = null!;
        [field: SerializeField] internal AvatarPreviewHeadIKSettings headIKSettings { get; private set; } = null!;

        // Resolved from the hierarchy so the shared prefab needs no new reference
        private Light GetPreviewLight() => previewLight ??= GetComponentInChildren<Light>(true);

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

            // A previous user (the lobby) may have switched the light off in favour of its own
            SetLightActive(true);

            // Rotation
            rotationTarget.rotation = Quaternion.identity;
            AngularVelocity = 0f;
            IsDragging = false;
            LastDragTime = 0f;

            // FOV
            TargetFOV = freeLookCamera.m_Lens.FieldOfView;
            fovTransitionStartTime = UnityEngine.Time.time;
            fovTransitionStartValue = freeLookCamera.m_Lens.FieldOfView;
            isFovTransitioning = false;

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

            StartFovTransition(preset.cameraFieldOfView);
        }

        public void StartFovTransition(float targetFov)
        {
            TargetFOV = targetFov;
            fovTransitionStartTime = UnityEngine.Time.time;
            fovTransitionStartValue = freeLookCamera.m_Lens.FieldOfView;
            isFovTransitioning = true;
        }

        public void SetPreviewPlatformActive(bool isActive) =>
            previewPlatform.SetActive(isActive);

        public void SetLightActive(bool isActive) =>
            GetPreviewLight().gameObject.SetActive(isActive);

        public void SetCameraFarClipPlane(float farClipPlane) =>
            freeLookCamera.m_Lens.FarClipPlane = farClipPlane;

        public void SetPostProcessingEnabled(bool isEnabled)
        {
            if (!camera.gameObject.TryGetComponent(out UniversalAdditionalCameraData cameraData))
            {
                ReportHub.LogError(ReportCategory.AVATAR, $"{nameof(UniversalAdditionalCameraData)} is missing on the character preview camera, post-processing can't be toggled");
                return;
            }

            cameraData.renderPostProcessing = isEnabled;
        }

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        [JetBrains.Annotations.UsedImplicitly] // Unity event function
        private void Update()
        {
            UpdateRotation();
            UpdateFov();
        }

        // The preview camera renders in the main scene, so the scene fog would tint the avatar by its distance to the camera
        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera != camera) return;

            fogBeforePreviewRender = RenderSettings.fog;
            RenderSettings.fog = false;
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera != camera) return;

            RenderSettings.fog = fogBeforePreviewRender;
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
                    return;
                }

                // Deceleration, higher inertia = faster deceleration
                float decelerationRate = RotationInertia * ANGULAR_VELOCITY_DECELERATION_COEFF * UnityEngine.Time.deltaTime;
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

                float rotationAmount = AngularVelocity * RotationModifier * UnityEngine.Time.deltaTime;

                rotation.y += rotationAmount;
                rotationTarget.rotation = Quaternion.Euler(rotation);
            }
        }

        private void UpdateFov()
        {
            float currentFov = freeLookCamera.m_Lens.FieldOfView;

            // Early return if already at target
            if (Mathf.Abs(currentFov - TargetFOV) <= FOW_LOWER_THRES)
            {
                isFovTransitioning = false;
                return;
            }

            float newFov;

            if (isFovTransitioning)
            {
                // Smooth transition for category changes
                float elapsedTime = UnityEngine.Time.time - fovTransitionStartTime;
                float normalizedTime = Mathf.Clamp01(elapsedTime / FOV_SMOOTH_TIME);

                float easedTime = Mathf.SmoothStep(0f, 1f, normalizedTime);
                newFov = Mathf.Lerp(fovTransitionStartValue, TargetFOV, easedTime);

                if (normalizedTime >= 1f)
                {
                    isFovTransitioning = false;
                    newFov = TargetFOV;
                }
            }
            else
            {
                // Smooth interpolation for scroll input
                float t = 1f - Mathf.Exp(-FOV_SPEED_COEFF * UnityEngine.Time.deltaTime);
                newFov = Mathf.Lerp(currentFov, TargetFOV, t);
            }

            freeLookCamera.m_Lens.FieldOfView = newFov;
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
            fovTransitionStartTime = UnityEngine.Time.time;
            fovTransitionStartValue = freeLookCamera.m_Lens.FieldOfView;
            isFovTransitioning = false;
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
