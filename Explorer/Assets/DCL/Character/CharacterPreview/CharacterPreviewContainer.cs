using Cinemachine;
using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace DCL.CharacterPreview
{
    public enum CharacterPreviewCameraMovementType
    {
        Pan,
        Rotate,
        Default
    }


    [Serializable]
    public struct CharacterPreviewCameraPreset
    {
        [field: SerializeField] internal Vector3 verticalPosition { get; private set; }
        [field: SerializeField] internal float cameraFieldOfView { get; private set; }
        [field: SerializeField] internal AvatarSlotCategoryEnum slotCategoryEnum { get; private set; }
    }

    /// <summary>
    ///     Contains serialized data only needed for the character preview
    ///     See CharacterPreviewController in the old renderer
    /// </summary>
    public class CharacterPreviewContainer : MonoBehaviour, IDisposable
    {
        [field: SerializeField] internal Vector3 previewPositionInScene { get; private set; }

        [field: SerializeField] internal Transform avatarParent { get; private set; }
        [field: SerializeField] internal new Camera camera { get; private set; }
        [field: SerializeField] internal Transform cameraTarget { get; private set; }
        [field: SerializeField] internal Transform rotationTarget { get; private set; }
        [field: SerializeField] internal CharacterPreviewCameraPreset[] cameraPositions { get; private set; }
        [field: SerializeField] internal float dragMovementModifier { get; private set; }
        [field: SerializeField] internal float scrollModifier { get; private set; }
        [field: SerializeField] internal float rotationModifier { get; private set; }
        [field: SerializeField] internal float minVerticalOffset { get; private set; }
        [field: SerializeField] internal float maxVerticalOffset { get; private set; }
        [field: SerializeField] internal float fieldOfViewThresholdForPanning { get; private set; }
        [field: SerializeField] internal float fieldOfViewThresholdForReCentering { get; private set; }
        [field: SerializeField] internal Vector2 depthLimits { get; private set; }
        [field: SerializeField] internal CinemachineFreeLook freeLookCamera { get; private set; }

        private Tween fovTween;

        public void Initialize(RenderTexture targetTexture)
        {
            this.transform.position = new Vector3(0, 5000, 0);
            camera.targetTexture = targetTexture;
            rotationTarget.rotation = Quaternion.identity;

            if (cameraPositions.Length > 0)
            {
                SetCameraPosition(cameraPositions[0]);
            }
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

        public void Dispose()
        {
            StopCameraTween();
        }
    }

}
