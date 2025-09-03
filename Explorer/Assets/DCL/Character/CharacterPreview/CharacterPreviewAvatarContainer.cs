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
        private const float MAX_ANGULAR_VELOCITY = 20f;
        private const float MAX_ROTATION_PER_FRAME = 10f;

        private Tween? fovTween;
        private float rotationAngularVelocity;
        private float currentRotationModifier;
        private float currentRotationInertia;
        private bool isDragging = false;
        private float lastDragTime = 0f;


        [field: SerializeField] internal Vector3 previewPositionInScene { get; private set; }
        [field: SerializeField] internal Transform avatarParent { get; private set; }
        [field: SerializeField] internal Camera camera { get; private set; }
        [field: SerializeField] internal Transform cameraTarget { get; private set; }
        [field: SerializeField] internal Transform rotationTarget { get; private set; }
        [field: SerializeField] internal CinemachineFreeLook freeLookCamera { get; private set; }
        [field: SerializeField] internal GameObject previewPlatform { get; private set; }
        [field: SerializeField] internal AvatarPreviewHeadIKSettings headIKSettings { get; private set; }

        public float RotationAngularVelocity => rotationAngularVelocity;

        public void Dispose()
        {
            StopCameraTweens();
        }

        public void Initialize(RenderTexture targetTexture, Vector3 position)
        {
            transform.position = position;
            camera.targetTexture = targetTexture;
            rotationTarget.rotation = Quaternion.identity;

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

        public void StopCameraTweens()
        {
            StopFOVTween();
            StopRotationTween();
        }

        public void StopFOVTween() =>
            fovTween?.Kill();

        public void StopRotationTween()
        {
            // rotationCancellationTokenSource?.Cancel();
            // rotationTask = null;
        }

        public void SetPreviewPlatformActive(bool isActive) =>
            previewPlatform.SetActive(isActive);

        public void SetRotationTween(float angularVelocity, float rotationModifier, float rotationInertia, Ease curve)
        {
            // Update the current angular velocity
            rotationAngularVelocity = Mathf.Clamp(
                angularVelocity,
                -MAX_ANGULAR_VELOCITY,
                MAX_ANGULAR_VELOCITY
            );

            // Store parameters for inertia calculation
            currentRotationModifier = rotationModifier;
            currentRotationInertia = rotationInertia;

            // Mark that we're currently dragging and update last drag time
            isDragging = true;
            lastDragTime = Time.time;
        }

        private void Update()
        {
            // Check if drag has ended (no SetRotationTween called for a few frames)
            if (isDragging && Time.time - lastDragTime > 0.1f) // 100ms threshold
            {
                isDragging = false;
            }

            // Early exit if no rotation is happening
            if (Mathf.Abs(rotationAngularVelocity) < 0.01f && !isDragging)
            {
                return;
            }

            // Apply rotation if there's any angular velocity
            if (Mathf.Abs(rotationAngularVelocity) > 0.01f)
            {
                Vector3 rotation = rotationTarget.rotation.eulerAngles;
                float rotationAmount = rotationAngularVelocity * currentRotationModifier;

                // Limit rotation per frame to prevent super-fast spinning
                rotationAmount = Mathf.Clamp(rotationAmount, -MAX_ROTATION_PER_FRAME, MAX_ROTATION_PER_FRAME);

                rotation.y += rotationAmount;
                rotationTarget.rotation = Quaternion.Euler(rotation);
            }

            // Apply inertia when not dragging
            if (!isDragging && currentRotationInertia > 0)
            {
                // Gradually reduce angular velocity based on inertia
                // Higher inertia = slower deceleration (takes longer to slow down)
                float inertiaDecay = Time.deltaTime * (1f / currentRotationInertia);
                rotationAngularVelocity = Mathf.Lerp(rotationAngularVelocity, 0f, inertiaDecay);

                // Stop when velocity is very low
                if (Mathf.Abs(rotationAngularVelocity) < 0.01f)
                {
                    rotationAngularVelocity = 0f;
                }
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
            StopCameraTweens();
            rotationTarget.rotation = Quaternion.identity;
            rotationAngularVelocity = 0f;
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
