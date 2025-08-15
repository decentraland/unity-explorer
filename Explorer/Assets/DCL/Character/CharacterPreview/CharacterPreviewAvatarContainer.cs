using Cinemachine;
using DG.Tweening;
using System;
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
        private Tween fovTween;
        private Tween? rotationTween;

        [field: SerializeField] internal Vector3 previewPositionInScene { get; private set; }
        [field: SerializeField] internal Transform avatarParent { get; private set; }
        [field: SerializeField] internal Camera camera { get; private set; }
        [field: SerializeField] internal Transform cameraTarget { get; private set; }
        [field: SerializeField] internal Transform rotationTarget { get; private set; }
        [field: SerializeField] internal CinemachineFreeLook freeLookCamera { get; private set; }
        [field: SerializeField] internal GameObject previewPlatform { get; private set; }
        [field: SerializeField] internal AvatarPreviewHeadIKSettings headIKSettings { get; private set; }

        private float smoothedDeltaX = 0f;

        public float SmoothedDeltaX => smoothedDeltaX;
        public Tween? RotationTween { get; set; }

        public void Dispose()
        {
            StopCameraTween();
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

            StopCameraTween();

            fovTween = DOTween.To(() => freeLookCamera.m_Lens.FieldOfView, x => freeLookCamera.m_Lens.FieldOfView = x, preset.cameraFieldOfView, 1)
                              .SetEase(Ease.OutQuad)
                              .OnComplete(() => fovTween = null);
        }

        public void StopCameraTween()
        {
            fovTween?.Kill();
        }

        public void SetPreviewPlatformActive(bool isActive) =>
            previewPlatform.SetActive(isActive);

        public void SetSmoothedDeltaX(float newDelta)
        {
            smoothedDeltaX = newDelta;
        }

        public void StopRotationTween() =>
            rotationTween?.Kill();
    }

    [Serializable]
    public class AvatarPreviewHeadIKSettings
    {
        public float MinAvatarDepth = 500;
        public float MaxAvatarDepth = 1500;

        public float HeadMoveSpeed = 0.5f;
    }
}
