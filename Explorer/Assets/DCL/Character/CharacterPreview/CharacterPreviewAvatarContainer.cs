using Cinemachine;
using Cysharp.Threading.Tasks;
using DG.Tweening;
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
        private Tween? fovTween;

        // Rotation system variables - minimized for efficiency
        private float angularVelocity; // degrees per second
        private float rotationModifier;
        private float rotationInertia;
        private AnimationCurve decelerationCurve;
        private bool isDragging = false;
        private float lastDragTime = 0f;
        private const float DRAG_TIMEOUT = 0.1f; // seconds

        [field: SerializeField] internal Vector3 previewPositionInScene { get; private set; }
        [field: SerializeField] internal Transform avatarParent { get; private set; }
        [field: SerializeField] internal Camera camera { get; private set; }
        [field: SerializeField] internal Transform cameraTarget { get; private set; }
        [field: SerializeField] internal Transform rotationTarget { get; private set; }
        [field: SerializeField] internal CinemachineFreeLook freeLookCamera { get; private set; }
        [field: SerializeField] internal GameObject previewPlatform { get; private set; }
        [field: SerializeField] internal AvatarPreviewHeadIKSettings headIKSettings { get; private set; }

        public float RotationAngularVelocity => angularVelocity;

        public void Dispose()
        {
            StopFOVTween();
        }

        public void Initialize(RenderTexture targetTexture, Vector3 position)
        {
            transform.position = position;
            camera.targetTexture = targetTexture;
            rotationTarget.rotation = Quaternion.identity;

            // Initialize rotation system
            angularVelocity = 0f;
            isDragging = false;
            lastDragTime = 0f;

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

            StopFOVTween();

            fovTween = DOTween.To(() => freeLookCamera.m_Lens.FieldOfView, x => freeLookCamera.m_Lens.FieldOfView = x, preset.cameraFieldOfView, 1)
                              .SetEase(Ease.OutQuad)
                              .OnComplete(() => fovTween = null);
        }

        public void StopFOVTween() =>
            fovTween?.Kill();

        public void SetPreviewPlatformActive(bool isActive) =>
            previewPlatform.SetActive(isActive);

        public void SetRotationTween(
            float inputDeltaX,
            float rotationModifier,
            float rotationInertia,
            AnimationCurve accelerationCurve,
            AnimationCurve decelerationCurve
        )
        {
            // Store current settings
            this.rotationModifier = rotationModifier;
            this.rotationInertia = rotationInertia;
            this.decelerationCurve = decelerationCurve;

            // Set dragging state and update last drag time
            isDragging = true;
            lastDragTime = Time.time;

            // Convert input delta (pixels per frame) to angular velocity (degrees per second)
            // This is framerate-independent: inputDeltaX is pixels moved this frame
            // We want degrees per second, so we divide by deltaTime to get the rate
            float targetVelocity = -inputDeltaX / Time.deltaTime;

            // Apply inertia when accelerating/decelerating
            if (rotationInertia <= 0f)
            {
                // No inertia - instant response
                angularVelocity = targetVelocity;
            }
            else
            {
                // Use inertia curve to control acceleration/deceleration
                float velocityDifference = targetVelocity - angularVelocity;
                float maxVelocity = Mathf.Max(Mathf.Abs(targetVelocity), Mathf.Abs(angularVelocity));
                float curveInput = maxVelocity > 0f ? Mathf.Abs(velocityDifference) / maxVelocity : 0f;

                float accelerationFactor = accelerationCurve.Evaluate(curveInput);
                float lerpSpeed = accelerationFactor * rotationInertia * Time.deltaTime;

                angularVelocity = Mathf.Lerp(angularVelocity, targetVelocity, lerpSpeed);
            }
        }

        public void StopDragging()
        {
            isDragging = false;
        }

        private void Update()
        {
            // Check if dragging has timed out (no drag input for DRAG_TIMEOUT seconds)
            if (isDragging && Time.time - lastDragTime > DRAG_TIMEOUT)
            {
                isDragging = false;
            }

            // When not dragging, decelerate towards zero
            if (!isDragging)
            {
                if (rotationInertia <= 0f)
                {
                    // No inertia - instant stop
                    angularVelocity = 0f;
                }
                else
                {
                    // Use inertia curve for deceleration
                    // Evaluate curve based on current velocity (0 = stopped, 1 = max speed)
                    float velocityMagnitude = Mathf.Abs(angularVelocity);
                    float maxVelocity = 100f; // Reasonable maximum for curve evaluation
                    float curveInput = Mathf.Clamp01(velocityMagnitude / maxVelocity);

                    float decelerationFactor = decelerationCurve.Evaluate(curveInput);
                    float lerpSpeed = decelerationFactor * rotationInertia * Time.deltaTime;

                    angularVelocity = Mathf.Lerp(angularVelocity, 0f, lerpSpeed);

                    // Force stop when velocity is very small to avoid floating point precision issues
                    if (Mathf.Abs(angularVelocity) < 0.01f)
                        angularVelocity = 0f;
                }
            }

            // Apply rotation if there's any angular velocity
            if (Mathf.Abs(angularVelocity) > 0.01f)
            {
                Vector3 rotation = rotationTarget.rotation.eulerAngles;

                // Apply rotation: degrees per second * deltaTime * modifier
                // This is framerate-independent because we're using degrees per second
                float rotationAmount = angularVelocity * rotationModifier * Time.deltaTime;

                rotation.y += rotationAmount;
                rotationTarget.rotation = Quaternion.Euler(rotation);
            }
        }

        public void SetFOVTween(float targetFOV, float duration, Ease curve)
        {
            StopFOVTween();

            fovTween = DOTween.To(
                                   () => freeLookCamera.m_Lens.FieldOfView,
                                   x => freeLookCamera.m_Lens.FieldOfView = x,
                                   targetFOV,
                                   duration
                               )
                              .SetEase(curve)
                              .OnComplete(() => { fovTween?.Kill(); });
        }

        public void ResetAvatarMovement()
        {
            StopFOVTween();
            rotationTarget.rotation = Quaternion.identity;
            angularVelocity = 0f;
            isDragging = false;
            lastDragTime = 0f;
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
