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
        private CinemachineComposer middleRigComposer;

        private Tween fovTween;
        private Tween xAxisValueTween;

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
            StopCameraTween();
        }

        public void Initialize(RenderTexture targetTexture)
        {
            transform.position = new Vector3(0, 5000, 0);
            camera.targetTexture = targetTexture;
            rotationTarget.rotation = Quaternion.identity;

            camera.gameObject.TryGetComponent(out UniversalAdditionalCameraData cameraData);

            //We disable post processing on all platforms as the shader is not working correctly and it shows a black background
            cameraData.renderPostProcessing = false;

            middleRigComposer = freeLookCamera.GetRig(1).GetCinemachineComponent<CinemachineComposer>();
        }

        public void SetCamera(CharacterPreviewCameraPreset preset)
        {
            StopCameraTween();

            if (cameraTarget != null)
                cameraTarget.localPosition = preset.verticalPosition;

            if (middleRigComposer != null)
            {
                middleRigComposer.m_ScreenX = preset.cameraScreenX;
                middleRigComposer.m_ScreenY = preset.cameraScreenY;
            }

            freeLookCamera.m_Orbits[1].m_Radius = preset.cameraMiddleRigRadius;

            TweenFovTo(preset.cameraFieldOfView, preset.cameraFieldOfViewInertiaDuration, preset.cameraFieldOfViewInertiaCurve);
        }

        public void StopCameraTween()
        {
            fovTween?.Kill();
            xAxisValueTween?.Kill();
        }

        public void SetPreviewPlatformActive(bool isActive) =>
            previewPlatform.SetActive(isActive);

        public void TweenFovTo(float targetValue, float duration, Ease ease)
        {
            StopCameraTween();

            fovTween = DOTween.To(
                                   () => freeLookCamera.m_Lens.FieldOfView,
                                   x => freeLookCamera.m_Lens.FieldOfView = x,
                                   targetValue,
                                   duration)
                              .SetEase(ease)
                              .OnComplete(() => fovTween = null);
        }

        public void TweenXAxisTo(float targetValue, float duration, Ease ease)
        {
            StopCameraTween();

            xAxisValueTween = DOTween.To(
                                          () => freeLookCamera.m_XAxis.Value,
                                          x => freeLookCamera.m_XAxis.Value = x,
                                          targetValue,
                                          duration)
                                     .SetEase(ease)
                                     .OnComplete(() => xAxisValueTween = null);
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
