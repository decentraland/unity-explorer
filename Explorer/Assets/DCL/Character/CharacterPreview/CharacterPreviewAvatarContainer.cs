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
        private const float ANGULAR_VELOCITY_DECELERATION_COEFF = 900f;

        private Tween? fovTween;

        [field: SerializeField] internal Vector3 previewPositionInScene { get; private set; }
        [field: SerializeField] internal Transform avatarParent { get; private set; }
        [field: SerializeField] internal Camera camera { get; private set; }
        [field: SerializeField] internal Transform cameraTarget { get; private set; }
        [field: SerializeField] internal Transform rotationTarget { get; private set; }
        [field: SerializeField] internal CinemachineFreeLook freeLookCamera { get; private set; }
        [field: SerializeField] internal GameObject previewPlatform { get; private set; }
        [field: SerializeField] internal AvatarPreviewHeadIKSettings headIKSettings { get; private set; }

        public float TargetFOV { get; set; }
        public float RotationModifier { get; set; }
        public float RotationInertia { get; set; }
        public bool IsDragging { get; set; }
        public float LastDragTime { get; set; }
        public float AngularVelocity { get; set; }

        public void Dispose()
        {
            StopFOVTween();
        }

        public void Initialize(RenderTexture targetTexture, Vector3 position)
        {
            transform.position = position;
            camera.targetTexture = targetTexture;
            rotationTarget.rotation = Quaternion.identity;
            AngularVelocity = 0f;
            IsDragging = false;
            LastDragTime = 0f;

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

        private void Update()
        {
            UpdateRotation();
            UpdateFOV();
        }

        private void UpdateRotation()
        {
            // Check if dragging has timed out (no drag input for DRAG_TIMEOUT seconds)
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

                // Linear deceleration: higher inertia = faster deceleration
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
            if (Mathf.Abs(AngularVelocity) > 0.01f)
            {
                Vector3 rotation = rotationTarget.rotation.eulerAngles;

                float rotationAmount = AngularVelocity * RotationModifier * Time.deltaTime;

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
            AngularVelocity = 0f;
            IsDragging = false;
            LastDragTime = 0f;
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
