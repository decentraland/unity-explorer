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
        private const float DRAG_TIMEOUT = 0.1f;
        private const float MAX_ANGULAR_VELOCITY = 900f;
        private const float ANGULAR_VELOCITY_DECELERATION_COEFF = 900f;

        private Tween? fovTween;
        private float angularVelocity;
        private float rotationModifier;
        private float rotationInertia;
        private bool isDragging;
        private float lastDragTime;

        [field: SerializeField] internal Vector3 previewPositionInScene { get; private set; }
        [field: SerializeField] internal Transform avatarParent { get; private set; }
        [field: SerializeField] internal Camera camera { get; private set; }
        [field: SerializeField] internal Transform cameraTarget { get; private set; }
        [field: SerializeField] internal Transform rotationTarget { get; private set; }
        [field: SerializeField] internal CinemachineFreeLook freeLookCamera { get; private set; }
        [field: SerializeField] internal GameObject previewPlatform { get; private set; }
        [field: SerializeField] internal AvatarPreviewHeadIKSettings headIKSettings { get; private set; }

        public void Dispose()
        {
            StopFOVTween();
        }

        public void Initialize(RenderTexture targetTexture, Vector3 position)
        {
            transform.position = position;
            camera.targetTexture = targetTexture;
            rotationTarget.rotation = Quaternion.identity;
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
            float rotationInertia
        )
        {
            this.rotationModifier = rotationModifier;
            this.rotationInertia = rotationInertia;

            isDragging = true;
            lastDragTime = Time.time;

            float targetVelocity = -inputDeltaX / Time.deltaTime;

            if (rotationInertia <= 0f)
            {
                // No inertia - instant response
                angularVelocity = targetVelocity;
            }
            else
            {
                // Linear acceleration: higher inertia = slower acceleration
                float accelerationRate = (1f / rotationInertia) * Time.deltaTime;
                angularVelocity = Mathf.Lerp(angularVelocity, targetVelocity, accelerationRate);
            }

            angularVelocity = Mathf.Clamp(angularVelocity, -MAX_ANGULAR_VELOCITY, MAX_ANGULAR_VELOCITY);
        }

        private void Update()
        {
            UpdateRotation();
            UpdateFOV();
        }

        private void UpdateRotation()
        {
            // Check if dragging has timed out (no drag input for DRAG_TIMEOUT seconds)
            if (isDragging && Time.time - lastDragTime > DRAG_TIMEOUT)
                isDragging = false;

            if (!isDragging)
            {
                if (rotationInertia <= 0f)
                {
                    angularVelocity = 0f;
                    return;
                }

                // Linear deceleration: higher inertia = faster deceleration
                float decelerationRate = rotationInertia * ANGULAR_VELOCITY_DECELERATION_COEFF * Time.deltaTime;
                float velocitySign = Mathf.Sign(angularVelocity);
                float velocityMagnitude = Mathf.Abs(angularVelocity);

                velocityMagnitude -= decelerationRate;

                if (velocityMagnitude <= 0f)
                    angularVelocity = 0f;
                else
                    angularVelocity = velocitySign * velocityMagnitude;
            }

            // Apply rotation if there's any angular velocity
            if (Mathf.Abs(angularVelocity) > 0.01f)
            {
                Vector3 rotation = rotationTarget.rotation.eulerAngles;

                float rotationAmount = angularVelocity * rotationModifier * Time.deltaTime;

                rotation.y += rotationAmount;
                rotationTarget.rotation = Quaternion.Euler(rotation);
            }
        }

        private void UpdateFOV()
        {
            // TODO convert from tween
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
