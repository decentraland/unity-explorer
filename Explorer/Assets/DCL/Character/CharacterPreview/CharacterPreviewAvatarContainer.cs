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
        private const float ANGULAR_VELOCITY_DIVISOR = 10f;

        private Tween? fovTween;
        private Tween? rotationTween;
        private float rotationAngularVelocity;

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

        public void SetCameraPosition(CharacterPreviewCameraPreset preset)
        {
            if (cameraTarget != null)
                cameraTarget.localPosition = preset.verticalPosition;

            StopCameraTweens();

            fovTween = DOTween.To(() => freeLookCamera.m_Lens.FieldOfView, x => freeLookCamera.m_Lens.FieldOfView = x, preset.cameraFieldOfView, 1)
                              .SetEase(Ease.OutQuad)
                              .OnComplete(() => fovTween = null);
        }

        public void StopCameraTweens()
        {
            fovTween?.Kill();
            rotationTween?.Kill();
        }

        public void SetPreviewPlatformActive(bool isActive) =>
            previewPlatform.SetActive(isActive);

        public void SetRotationTween(float angularVelocity, float rotationModifier, Ease curve)
        {
            rotationAngularVelocity = angularVelocity;

            rotationTween.Kill();

            rotationTween = DOTween.To(
                                        () => rotationAngularVelocity,
                                        x => rotationAngularVelocity = x,
                                        0f,
                                        Mathf.Abs(rotationAngularVelocity) / ANGULAR_VELOCITY_DIVISOR // duration based on velocity (tweak divisor)
                                    )
                                   .SetEase(curve)
                                   .OnUpdate(() =>
                                    {
                                        Vector3 rotation = rotationTarget.rotation.eulerAngles;
                                        rotation.y += rotationAngularVelocity * rotationModifier; // apply rotationModifier here
                                        rotationTarget.rotation = Quaternion.Euler(rotation);
                                    })
                                   .OnComplete(() =>
                                    {
                                        rotationAngularVelocity = 0f;
                                        rotationTween.Kill();
                                    });
        }

        public void SetFOVTween(float targetFOV, float duration, Ease curve)
        {
            fovTween?.Kill();

            fovTween = DOTween.To(
                () => freeLookCamera.m_Lens.FieldOfView,
                x => freeLookCamera.m_Lens.FieldOfView = x,
                targetFOV,
                duration
                )
               .SetEase(curve)
               .OnComplete(() =>
                {
                    fovTween?.Kill();
                });
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
