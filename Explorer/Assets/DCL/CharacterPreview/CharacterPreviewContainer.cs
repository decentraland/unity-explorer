using Cinemachine;
using DCL.CharacterCamera.Settings;
using System;
using UnityEngine;

namespace DCL.CharacterPreview
{
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
    public class CharacterPreviewContainer : MonoBehaviour
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

        public void Initialize(RenderTexture targetTexture)
        {
            this.transform.position = new Vector3(0, 5000, 0);
            camera.targetTexture = targetTexture;
            rotationTarget.rotation = Quaternion.identity;
            SetCameraPosition(cameraPositions[0]);
        }

        public void SetCameraPosition(CharacterPreviewCameraPreset preset)
        {
            cameraTarget.localPosition = preset.verticalPosition;
            freeLookCamera.m_Lens.FieldOfView = preset.cameraFieldOfView; //We need to animate FOV setting from previous to next
        }
    }

}
