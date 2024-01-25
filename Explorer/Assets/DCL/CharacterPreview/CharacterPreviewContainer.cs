using Cinemachine;
using DG.Tweening;
using System;
using UnityEngine;

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

        [field: SerializeField] internal float maxHorizontalOffset { get; private set; }
        [field: SerializeField] internal float minVerticalOffset { get; private set; }
        [field: SerializeField] internal float maxVerticalOffset { get; private set; }

        [field: SerializeField] internal Vector2 depthLimits { get; private set; }
        [field: SerializeField] internal CinemachineFreeLook freeLookCamera { get; private set; }
        [field: SerializeField] internal Texture2D rotateCursor { get; private set; }
        [field: SerializeField] internal Vector2 rotateCursorCenter { get; private set; }
        [field: SerializeField] internal Texture2D panCursor { get; private set; }
        [field: SerializeField] internal Vector2 panCursorCenter { get; private set; }


        internal Texture2D defaultCursor { get; private set; }

        private Tween fovTween;

        public void Initialize(RenderTexture targetTexture)
        {
            this.transform.position = new Vector3(0, 5000, 0);
            camera.targetTexture = targetTexture;
            camera.clearFlags = CameraClearFlags.Depth; // or CameraClearFlags.SolidColor
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f); // Transparent background color
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

        public void SetCursor(CharacterPreviewCameraMovementType movementType)
        {
            switch (movementType)
            {
                case CharacterPreviewCameraMovementType.Pan:
                    Cursor.SetCursor(panCursor, panCursorCenter, CursorMode.Auto);
                    break;
                case CharacterPreviewCameraMovementType.Rotate:
                    Cursor.SetCursor(rotateCursor, rotateCursorCenter, CursorMode.Auto);
                    break;
                case CharacterPreviewCameraMovementType.Default:
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    break;
            }
        }
    }

}
