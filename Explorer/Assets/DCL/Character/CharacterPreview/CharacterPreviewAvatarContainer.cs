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
        [field: SerializeField] internal Vector3 previewPositionInScene { get; private set; }
        [field: SerializeField] internal Transform avatarParent { get; private set; }
        [field: SerializeField] internal Camera camera { get; private set; }
        [field: SerializeField] internal Transform cameraTarget { get; private set; }
        [field: SerializeField] internal Transform rotationTarget { get; private set; }
        [field: SerializeField] internal CinemachineFreeLook freeLookCamera { get; private set; }
        [field: SerializeField] internal GameObject previewPlatform { get; private set; }

        public void Dispose()
        {
            StopCameraTween();
        }

        public void Initialize(RenderTexture targetTexture)
        {
            transform.position = new Vector3(0, 5000, 0);
            camera.targetTexture = targetTexture;
            rotationTarget.rotation = Quaternion.identity;

#if UNITY_STANDALONE_OSX
            camera.gameObject.TryGetComponent(out UniversalAdditionalCameraData cameraData);
            if (cameraData)
            {
            //We disable post processing on OSX as the shader is not working correctly and it shows a black background
                cameraData.renderPostProcessing = false;
            }
#endif
        }

        public void SetCameraPosition(CharacterPreviewCameraPreset preset)
        {
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
    }
}
